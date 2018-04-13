dotnet restore;
dotnet build -c Release;
# dotnet pack --no-build -c Release Davenport/Davenport.fsproj;
./.paket/paket.exe pack "nuget"

$nupkg = (gci "nuget/*.nupkg")[0];

# Push the nuget package to AppVeyor's artifact list.
Push-AppveyorArtifact $nupkg.FullName -FileName $nupkg.Name -DeploymentName "davenport.nupkg";
