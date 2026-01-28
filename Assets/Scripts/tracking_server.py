import asyncio
import json
import time
from typing import List, Dict

import cv2
import numpy as np
import pyrealsense2 as rs  # Intel RealSense
from ultralytics import YOLO
import websockets


# -----------------------------
# 1. Camera & YOLO setup
# -----------------------------

# Your RealSense camera serial
REALSENSE_SERIAL = "046222071238"

# WebSocket server settings
WS_HOST = "localhost"
WS_PORT = 8765

# Load YOLOv8 pose model
print("Loading YOLOv8-pose model...")
# model = YOLO("yolov8x-pose.pt")
model = YOLO("yolov8n-pose.pt")
model.to("cpu")  # change to "cuda" if using GPU
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

    pipeline.start(config)
    align_to_color = rs.align(rs.stream.color)

    time.sleep(1.0)
    print("RealSense ready.")
    return pipeline, align_to_color


pipeline, align = setup_realsense(REALSENSE_SERIAL)


# -----------------------------
# 2. Build HumanData list
# -----------------------------

def build_humans_from_frame(
    color_image: np.ndarray,
    depth_image: np.ndarray,
    frame_index: int
) -> List[Dict]:
    """
    Run YOLO tracking and build a list matching
    NetworkManager.HumanData in Unity.
    """
    humans: List[Dict] = []

    results = model.track(color_image, **TRACK_ARGS)
    if not results:
        return humans

    res = results[0]
    if res.boxes is None or res.boxes.xyxy is None:
        return humans

    boxes_xyxy = res.boxes.xyxy.cpu().numpy()
    confidences = res.boxes.conf.cpu().numpy().tolist()

    track_ids = (
        res.boxes.id.int().cpu().numpy().tolist()
        if res.boxes.id is not None
        else []
    )

    timestamp_ms = int(time.time() * 1000)
    h_img, w_img, _ = color_image.shape

    for i, box in enumerate(boxes_xyxy):
        if i >= len(track_ids):
            continue

        x1, y1, x2, y2 = box.astype(int)
        person_id = int(track_ids[i])
        conf = float(confidences[i])

        # Clamp bbox to image bounds
        x1 = max(0, min(x1, w_img - 1))
        x2 = max(0, min(x2, w_img - 1))
        y1 = max(0, min(y1, h_img - 1))
        y2 = max(0, min(y2, h_img - 1))

        center_x = (x1 + x2) // 2
        center_y = (y1 + y2) // 2
        w_box = x2 - x1
        h_box = y2 - y1

        # Depth sampling (median of small region)
        depth_value = 0.0
        if 0 <= center_x < depth_image.shape[1] and 0 <= center_y < depth_image.shape[0]:
            region = depth_image[
                max(0, center_y - 3):min(depth_image.shape[0], center_y + 3),
                max(0, center_x - 3):min(depth_image.shape[1], center_x + 3)
            ]
            valid_depths = region[region > 0]
            if valid_depths.size > 0:
                depth_value = float(np.median(valid_depths))

        human = {
            "person_id": person_id,
            "x": float(center_x),
            "y": float(center_y),
            "w": float(w_box),
            "h": float(h_box),
            "depth": depth_value,
            "confidence": round(conf, 3),
            "frame": frame_index,
            "facing_screen": False,
            "wave_detected": False,
            "server_ts_ms": timestamp_ms,
            "keypoint_motions": [],
        }

        humans.append(human)

    return humans


# -----------------------------
# 3. WebSocket server
# -----------------------------

async def tracking_handler(websocket):
    """
    Continuously:
    - Grab RealSense frames
    - Run YOLO tracking
    - Send HumanData JSON to Unity
    """
    print("Unity connected to WebSocket.")
    frame_index = 0

    try:
        while True:
            frames = pipeline.wait_for_frames()
            frames = align.process(frames)

            color_frame = frames.get_color_frame()
            depth_frame = frames.get_depth_frame()
            if not color_frame or not depth_frame:
                continue

            color_image = np.asanyarray(color_frame.get_data())
            depth_image = np.asanyarray(depth_frame.get_data())

            # Mirror for screen alignment
            color_image = cv2.flip(color_image, 1)
            depth_image = cv2.flip(depth_image, 1)

            humans = build_humans_from_frame(color_image, depth_image, frame_index)
            frame_index += 1

            if humans:
                await websocket.send(json.dumps(humans))

            await asyncio.sleep(0.03)

    except websockets.ConnectionClosed:
        print("Unity disconnected.")
    except Exception as e:
        print(f"Error in tracking_handler: {e}")


async def main():
    print(f"Starting WebSocket server on ws://{WS_HOST}:{WS_PORT}")
    async with websockets.serve(tracking_handler, WS_HOST, WS_PORT):
        await asyncio.Future()  # run forever


if __name__ == "__main__":
    try:
        asyncio.run(main())
    finally:
        print("Stopping RealSense pipeline...")
        try:
            pipeline.stop()
        except Exception:
            pass
