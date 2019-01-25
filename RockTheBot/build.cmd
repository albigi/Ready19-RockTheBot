REM Build App
ECHO OFF
set dir=RockTheBot
cd %dir%
dotnet publish -c Release -o ..\bin\Release\

REM Build Docker Img
cd ..\
docker stop %dir%
docker rm %dir%
docker build -t %dir% .
docker run -d -p 80:80 --name %dir% %dir%