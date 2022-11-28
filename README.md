# IP WebcCam-viewer
Web Viewer and controller for my IP WebCam.

[![Build status](https://dev.azure.com/bradut/IP-WebCam/_apis/build/status/IP-WebCam-ASP.NET-CI)](https://dev.azure.com/bradut/IP-WebCam/_build/latest?definitionId=11)


This project allows viewing images from an IP WebCam, as well as controlling its orientation and zoom.

## Features:
- Fetches jpeg images from camera 
- Controls camera's positioning and zooming: Pan-Tilt-Zoom (PTZ)

## Implementation details:

- Uses the [Leader Election pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/leader-election) to allow only one instance to get data from the WebCam and cache it.<br />
        This improves the performance of the application and reduces the load on the camera.
		
- Images are cached in order to improve performance and reduce internet traffic		

- Camera uses Basic Authentication

- WebConfig app settings stores sensitive info in the file **settings.secrets**, which you will need to create from file **settings.secret.SAMPLE** and fill it with your values:
    - key="**MediaServerUrl**" value="http://12.34.56.78" -> your external IP
    - key="**MediaServerPort**" value="1234" -> your external port, which is forwarded in your local network to camera's IP and port
    - key="**MediaServerUsername**" value="user_name" -> camera's username
    - key="**MediaServerPassword**" value="pass_word" -> camera's password



## Note for developers:  
When running this app first time, if your browser displays this error:
``
Could not find a part of the path '<....>\IpWebCam3\bin\roslyn\csc.exe'``<br/>
Then run this in the Package Manager Console:
``
Update-Package Microsoft.CodeDom.Providers.DotNetCompilerPlatform -r
``

<!--## Known issues:
- User experience with PTZ is impacted by poor internet connections-->
  
## ToDos:
### User Experience ToDos:
  - Hide PTZ controls from non-admin users
  - Use sliders instead of arrows for absolute positioning
  - Allow using pre-defined preset-points instead of using PTZ controls
  - Privacy: avoid broadcasting from certain private areas

### Implementation ToDos:
 - Use a better caching mechanism  
 - Increase testing coverage
 - Use ONVIF API (besides CGI API)
