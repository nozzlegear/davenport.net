# Install CouchDB with one-get's msi installer.
curl https://dl.bintray.com/apache/couchdb/win/2.0.0.1/apache-couchdb-2.0.0.1.msi -UseBasicParsing -outfile couchdb.msi;
install-package -providername msi ./couchdb.msi;

# Try to connect to CouchDB to check that it installed.
$couchOutput = curl http://localhost:5984 -UseBasicParsing;

if ($couchOutput.StatusCode -ne 200) {
    echo $couchOutput;
    throw "CouchDB did not return a 200 OK status code. Did it fail to install?";
}

$testProj = "Davenport.Tests/Davenport.Tests.csproj";
$tests = "Parser", "Config", "Client";

# Execute all tests, throwing when one fails.
$tests | % {
    $output = dotnet test -c Release $testProj --filter Category=$_;

    echo $output;

    if ($LastExitCode -ne 0 -or $output -contains "Test Run Failed.") {
        throw "$testName tests failed with exit code $LastExitCode.";
    }
}