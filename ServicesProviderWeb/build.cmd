ECHO OFF
for %%I in (.) do set dir=%%~nxI
cd %dir%
dotnet publish -c Release -o ..\bin\Release\

cd ..\
docker stop %dir%
docker rm %dir%
docker build -t %dir% .
docker run -d -p 80:80 --name %dir% %dir%