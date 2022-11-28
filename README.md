# IP WebcCam-viewer
Web Viewer and controller for my IP WebCam.

[![Build status](https://dev.azure.com/bradut/IP-WebCam/_apis/build/status/IP-WebCam-ASP.NET-CI)](https://dev.azure.com/bradut/IP-WebCam/_build/latest?definitionId=11)


This project allows viewing images from an IP WebCam, as well as controlling its orientation and zoom.

## Features:
- Fetches jpeg images from camera 
- Controls camera's positioning and zooming: Pan-Tilt-Zoom (PTZ)

## Implementation details:
- Programming: C#/WebAPI/JavaScript
- WebConfig app settings stores sensitive info in the file **settings.secrets**:
  You neeed to create settings.secret from file **settings.secret.SAMPLE** and fill it with your values:
    - key="MediaServerUrl" value="http://12.34.56.78" -> your external IP
    - key="MediaServerPort" value="1234" -> your external port, which is forwarded in your local network to camera's IP and port
    - key="MediaServerUsername" value="user_name" -> camera's username
    - key="MediaServerPassword" value="pass_word" -> camera's password
- Camera uses Basic Authentication
- Images are cached in order to improve performance and reduce internet traffic

## Note for developers:  
When running this app first time, if your browser displays this error:
``
Could not find a part of the path '<....>\IpWebCam3\bin\roslyn\csc.exe'``<br/>
Then run this in the Package Manager Console:
``
Update-Package Microsoft.CodeDom.Providers.DotNetCompilerPlatform -r
``

## Known issues:
- User experience with PTZ is impacted by poor internet connections
  
## ToDos:
### User Experience:
  - hide PTZ controls from non-admin users
  - use sliders instead of arrow, for absolute positioning
  - follow pre-defined preset-points instead of using PTZ controls
  - privacy: Should not broadcast images from certain private areas

### Implementation:
 - Use a better caching mechanism  
 - Increase testing coverage
 - Use ONVIF API (besides CGI API)
