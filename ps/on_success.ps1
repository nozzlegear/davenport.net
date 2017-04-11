    dotnet pack --no-build -c Release Davenport/davenport.csproj;

    $nupkg = (gci Davenport/bin/Release/*.nupkg)[0];
    
    Push-AppveyorArtifact $nupkg.FullName -FileName $nupkg.Name -DeploymentName $nupkg.Name;