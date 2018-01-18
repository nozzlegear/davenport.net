dotnet restore;
dotnet build -c Release;
dotnet pack --no-build -c Release Davenport/davenport.csproj;
dotnet pack --no-build -c Release Davenport.Fsharp/Davenport.Fsharp.fsproj

$csnupkg = (gci "Davenport/bin/Release/*.nupkg")[0];
$fsnupkg = (gci "Davenport.Fsharp/bin/Release/*.nupkg")[0];

# Push the nuget package to AppVeyor's artifact list.
Push-AppveyorArtifact $csnupkg.FullName -FileName $csnupkg.Name -DeploymentName "davenport.nupkg";
Push-AppveyorArtifact $fsnupkg.FullName -FileName $fsnupkg.Name -DeploymentName "davenport.fsharp.nupkg"