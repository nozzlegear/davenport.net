# Install CouchDB with one-get's msi installer.
curl https://dl.bintray.com/apache/couchdb/win/2.0.0.1/apache-couchdb-2.0.0.1.msi -UseBasicParsing -outfile couchdb.msi;
install-package -providername msi ./couchdb.msi;

# Try to connect to CouchDB to check that it installed.
$couchOutput = curl http://localhost:5984 -UseBasicParsing;

if ($couchOutput.StatusCode -ne 200) {
    echo $couchOutput;
    throw "CouchDB did not return a 200 OK status code. Did it fail to install?";
}

$fsTestProj = "Davenport.Fsharp.Tests/Davenport.Fsharp.Tests.fsproj"
$csTestProj = "Davenport.Tests/Davenport.Tests.csproj";
$csTests = "Parser", "Config", "Client";

# Execute all C# tests, throwing when one fails.
$csTests | % {
    $output = dotnet test -c Release $csTestProj --filter Category=$_;

    echo $output;

    if ($LastExitCode -ne 0 -or $output -contains "Test Run Failed.") {
        throw "$testName tests failed with exit code $LastExitCode.";
    }
}

# Execute F# tests with --debug flag which prints test execution length details
$output = dotnet run -c Release -p $fsTestProj --debug

echo $output

if ($LastExitCode -ne 0 -or $output -contains "Test Run Failed.") {
    throw "F# tests failed with exit code $LastExitCode.";
}