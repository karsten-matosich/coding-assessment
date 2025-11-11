Database will be created with 3 accounts:
  Name       Account #  Balance
  Account 1, 1          $1,000.00
  Account 2, 2          $2,000.00
  Account 3, 3          $3,000.00


A CSV file called test-transactions.csv containing 50 test transactions has been included for convenience
A second CSV file called golden.csv is an intentionally slightly modified version of the test-transactions.csv file for the purpose of generating a sample reconciliation report


In /database please run:
  docker build -t my-database-image .
  docker run -d --name database-container -v karsten-assessment-db-data:/data my-database-image


In /back-end please run:
  docker build -t my-api-image .
  docker run -d --name backend-container -p 5001:80 -v karsten-assessment-db-data:/data my-api-image


.NET SDK is required to run unit tests. If it is not already installed on your machine, you can download it here: https://dotnet.microsoft.com/download

To run unit tests, in /back-end/KarstenAssessmentApi.Tests please run: 
  dotnet test


In /front-end please run:
  docker build -t my-angular-image .
  docker run -d --name frontend-container -p 4200:80 my-angular-image