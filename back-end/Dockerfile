FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["KarstenAssessmentApi/KarstenAssessmentApi.csproj", "./"]
RUN dotnet restore "./KarstenAssessmentApi.csproj"

COPY ["KarstenAssessmentApi/", "./"]
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
RUN mkdir -p /data

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

ENTRYPOINT ["dotnet", "KarstenAssessmentApi.dll"]

ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

ENTRYPOINT ["dotnet", "KarstenAssessmentApi.dll"]