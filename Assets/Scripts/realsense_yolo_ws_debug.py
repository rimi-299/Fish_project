import asyncio
import json
import time
from typing import List, Dict

import cv2
import numpy as np
import pyrealsense2 as rs  # Intel RealSense
from ultralytics import YOLO
import websockets
import threading


# -----------------------------
# 0. Settings
# -----------------------------

# Your RealSense camera serial
REALSENSE_SERIAL = "046222071238"

# WebSocket server settings
WS_HOST = "localhost"
WS_PORT = 8765

# Enable/disable YOLO debug visualization window
SHOW_DEBUG_WINDOW = True


# ----------------------------- 
# Globals for debug window
# -----------------------------

debug_frame = None
debug_lock = threading.Lock()


def debug_window_thread():
    """
    Runs in a separate thread so OpenCV GUI does not block asyncio.
    It just displays the latest frame stored in `debug_frame`.
    """
    cv2.namedWindow("YOLO Debug", cv2.WINDOW_NORMAL)
    cv2.resizeWindow("YOLO Debug", 1280, 720)

    while True:
        with debug_lock:
            frame_copy = None if debug_frame is None else debug_frame.copy()

        if frame_copy is not None:
            cv2.imshow("YOLO Debug", frame_copy)

        # Press 'q' to close the debug window (server keeps running)
        if cv2.waitKey(10) & 0xFF == ord('q'):
            break

    cv2.destroyAllWindows()


# -----------------------------
# 1. Camera & YOLO setup
# -----------------------------

print("Loading YOLOv8 pose model...")
# Choose one model:
# model = YOLO("yolov8x-pose.pt")
model = YOLO("yolov8n-pose.pt")  # smaller & faster

# Use GPU if available; otherwise keep "cpu"
# model.to("cuda")
model.to("cpu")
print("Model loaded.")

# Tracking parameters (ByteTrack)
TRACK_ARGS = {
    "tracker": "bytetrack.yaml",
    "persist": True,
    "conf": 0.5,
    "iou": 0.5,
    "classes": 0,  # class 0 = person
}


def setup_realsense(serial: str):
    """
    Initialize a single RealSense camera (color + depth).
    """
    print(f"Initializing RealSense camera {serial}...")
    pipeline = rs.pipeline()
    config = rs.config()

    config.enable_device(serial)
    config.enable_stream(rs.stream.color, 640, 480, rs.format.bgr8, 30)
    config.enable_stream(rs.stream.depth, 640, 480, rs.format.z16, 30)

    profile = pipeline.start(config)
    align_to_color = rs.align(rs.stream.color)

    time.sleep(1.0)
    print("RealSense ready.")
    return pipeline, align_to_color


pipeline, align = setup_realsense(REALSENSE_SERIAL)


# -----------------------------
# 2. Helper: build HumanData list
#    + create YOLO debug image
# -----------------------------

def build_humans_from_frame(
    color_image: np.ndarray,
    depth_image: np.ndarray,
    frame_index: int,
):
    """
    Run YOLO tracking on the color frame and build:
      - list of HumanData-like dicts (for Unity)
      - annotated debug image (for OpenCV window)

    Matches the C# NetworkManager.HumanData structure.
    """
    humans: List[Dict] = []

    # Run YOLO with tracking
    results = model.track(color_image, **TRACK_ARGS)

    if results is None or len(results) == 0:
        return humans, color_image  # no detections; show raw frame

    res = results[0]

    # Prepare annotated frame using YOLO's built-in plot
    try:
        annotated = res.plot()
    except Exception:
        annotated = color_image.copy()

    # If there are no boxes, nothing to send
    if res.boxes is None or res.boxes.xyxy is None or len(res.boxes) == 0:
        return humans, annotated

    boxes_xyxy = res.boxes.xyxy.cpu().numpy()
    confidences = res.boxes.conf.cpu().numpy().tolist()
    track_ids = res.boxes.id.int().cpu().numpy().tolist() if res.boxes.id is not None else []
    keypoints = (
        res.keypoints.xy.cpu().numpy()
        if hasattr(res, "keypoints") and res.keypoints is not None
        else None
    )

    timestamp_ms = int(time.time() * 1000)

    for i, box in enumerate(boxes_xyxy):
        if i >= len(track_ids) or i >= len(confidences):
            continue

        x1, y1, x2, y2 = box.astype(int)
        person_id = int(track_ids[i])
        conf = float(confidences[i])

        # Clamp bbox within the image
        h_img, w_img, _ = color_image.shape
        x1 = max(0, min(x1, w_img - 1))
        x2 = max(0, min(x2, w_img - 1))
        y1 = max(0, min(y1, h_img - 1))
        y2 = max(0, min(y2, h_img - 1))

        # Center of bbox
        center_x = (x1 + x2) // 2
        center_y = (y1 + y2) // 2
        w_box = x2 - x1
        h_box = y2 - y1

        # Get depth at center (median of small region for stability)
        depth_value = 0.0
        if 0 <= center_x < depth_image.shape[1] and 0 <= center_y < depth_image.shape[0]:
            y_min = max(0, center_y - 3)
            y_max = min(depth_image.shape[0], center_y + 3)
            x_min = max(0, center_x - 3)
            x_max = min(depth_image.shape[1], center_x + 3)
            region = depth_image[y_min:y_max, x_min:x_max]
            valid_depths = region[region > 0]
            if valid_depths.size > 0:
                depth_value = float(np.median(valid_depths))

        # Flags you're not using yet
        facing_screen = False
        wave_detected = False
        keypoint_motions = []

        human = {
            "person_id": person_id,
            "x": float(center_x),
            "y": float(center_y),
            "w": float(w_box),
            "h": float(h_box),
            "depth": float(depth_value),
            "confidence": float(round(conf, 3)),
            "frame": int(frame_index),
            "facing_screen": bool(facing_screen),
            "wave_detected": bool(wave_detected),
            "server_ts_ms": int(timestamp_ms),
            "keypoint_motions": keypoint_motions,
        }

        humans.append(human)

        # Optional: draw extra ID text on top of YOLO's plot
        cv2.putText(
            annotated,
            f"ID {person_id}",
            (x1, max(y1 - 10, 0)),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.5,
            (0, 255, 255),
            1,
            cv2.LINE_AA,
        )

    return humans, annotated


# -----------------------------
# 3. WebSocket server handler
# -----------------------------

async def tracking_handler(websocket):
    """
    For each connected Unity client:
    - Continuously grab frames from RealSense
    - Run YOLO tracking
    - Send HumanData list as JSON
    - Update debug_frame for visualization thread
    """
    print("Unity connected to WebSocket.")

    frame_index = 0

    try:
        while True:
            # Get frames from RealSense
            frames = pipeline.wait_for_frames()
            frames = align.process(frames)

            color_frame = frames.get_color_frame()
            depth_frame = frames.get_depth_frame()
            if not color_frame or not depth_frame:
                continue

            # Convert to numpy arrays
            color_image = np.asanyarray(color_frame.get_data())
            depth_image = np.asanyarray(depth_frame.get_data())

            # OPTIONAL: flip horizontally so movement matches screen
            color_image = cv2.flip(color_image, 1)
            depth_image = cv2.flip(depth_image, 1)

            # Build HumanData-like list and get annotated debug image
            humans, annotated = build_humans_from_frame(color_image, depth_image, frame_index)
            frame_index += 1

            # Send data to Unity
            if humans:
                await websocket.send(json.dumps(humans))

            # Update debug frame for the OpenCV thread
            if SHOW_DEBUG_WINDOW and annotated is not None:
                with debug_lock:
                    global debug_frame
                    debug_frame = annotated

            # Small delay to avoid 100% CPU and match ~30 FPS
            await asyncio.sleep(0.03)

    except websockets.ConnectionClosed:
        print("Unity disconnected.")
    except Exception as e:
        print(f"Error in tracking_handler: {e}")


async def main():
    print(f"Starting WebSocket server on ws://{WS_HOST}:{WS_PORT}")
    async with websockets.serve(tracking_handler, WS_HOST, WS_PORT):
        await asyncio.Future()  # Run forever


# -----------------------------
# 4. Entry point
# -----------------------------

if __name__ == "__main__":
    try:
        # Start debug window thread if enabled
        if SHOW_DEBUG_WINDOW:
            threading.Thread(
                target=debug_window_thread,
                daemon=True
            ).start()

        asyncio.run(main())

    finally:
        print("Stopping RealSense pipeline...")
        try:
            pipeline.stop()
        except Exception:
            pass

        if SHOW_DEBUG_WINDOW:
            # In case the thread already closed the window, this is harmless
            cv2.destroyAllWindows()
