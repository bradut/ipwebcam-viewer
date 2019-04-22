# ipwebcam-viewer
Web Viewer for my IP WebCam: PZT, C#

This project allows viewing real-time images from an IP WebCam.

Features (2019-04-22):
- Fetches jpeg images from camera (not video streaming yet)
- Allows users to control the camera: Pan-Tilt-Zoom (PTZ)

Implementation details:
- Programming: C#/WebAPI/JavaScript
- WebConfig app settings:
  - key="MediaServerUrl" value="http://12.34.56.78" -> your external IP
  - key="MediaServerPort" value="1234" -> your port, forwarded internally to camera's IP and port
  - key="MediaServerUsername" value="user_name" -> camera's username
  - key="MediaServerPassword" value="pass_word" -> camera's password
- Camera uses Basic Authentication
- Images are cached in order to improve performance and reduce internet traffic

Known issues:
- User experience with PTZ is impacted by poor internet connections
  
ToDos:
- User Experience:
  - hide PTZ controls from non-admin users
  - instead of PTZ controls, have the camera follow pre-defined preset-points
  - privacy: do not broadcast images from certain private areas
- Implementation:
  - Use Dependency Injection
  - Increase testing coverage
  - Use ONVIF API (besides CGI API)
