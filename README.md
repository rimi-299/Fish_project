# Fish Project
This file provides an overview and necessary information for understanding and implementing the project using unity and python scripts.

# Project Overview

This study explores how ambient interactions with large displays in public spaces affect user engagement and experience. We aim to understand how virtual fish rendered on large displays in the Goldberg Computer Science Building at Dalhousie University and on Barrington Street influence people’s interactions. Using Intel RealSense cameras and computer vision, we track individuals in real-time and pass this data to Unity, a game development engine, to render virtual fish. The study tests four scenarios:  
Following fish: Fish follow and react directly to people's movements.  
Independent fish: Fish move autonomously, independent of human movement.  
Mixed scenario: A combination of both following and independent fish.  
Empty scenario: No fish are displayed.  
These scenarios will switch every few hours over a one-month period.  
The rationale is to investigate how such ambient technologies can enhance user experience in public settings. The purpose is to compare engagement levels across the four scenarios. The study population includes members of the Dalhousie University community who naturally interact with the displays in the Goldberg Building, as well as members of the general public who use the sidewalk and pass by the display on the window of the Paramount Building on Barrington Street. No specific recruitment is needed since participation is natural and unprompted.  

# Folder Structure

The project folder is organized as follows:  
.vscode/: Contains json files.  
Assets/scripts: Holds all scripts.  
Packages/: Includes Json files with the dependencies.  
ProjectSettings/: Contains additional package files, such as unity assets.  

# Getting Started

To get started with this project, follow these steps:  
Download Python 3.11.9 version at https://www.python.org/downloads/release/python-3119/. (Note: the version HAS to be 3.11)  
Install Pyrealsense2 on your machine by typing “py -m pip install pyrealsense2” on the command prompt or terminal.  
Install Ultralytics on your machine by typing “py -m pip install ultralytics” on the command prompt or terminal.  
Install cv2 on your machine by typing “py -m pip install opencv-python” on the command prompt or terminal.  
Install websockets on your machine by typing “py -m pip install websockets” on the command prompt or terminal.  
Download unity hub and unity version 2022 3.4.6 at https://unity.com/releases/editor/whats-new/2022.3.46#installs   

# Instructions
To open the project, open unity hub -> select Projects on the left tab -> click add -> add project from -> select the folder of the repo downloaded on your computer.  

After opening the project, make sure the FishTank scene is selected by going to Assets in the Project tab below and clicking Scenes.   

Next to run the simulation, go back to Assets -> Scripts -> open logging_triple_camera_wave_server.py -> find where the serial numbers are stated as “desired_order = [
    "048122072514",
    "048122072443",
    "048322070198"
]” and add or comment out the necessary serial number of the camera you are using.  -> run the script (Note: make sure the yolo version you are using is yolov8n and not yolov8x by looking for the line ‘model = YOLO(...)’)  

While running the script, go back to the unity tab and press play on the top of the application tab.  

