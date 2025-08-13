# Use SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY MyS3App.csproj ./
RUN dotnet restore

# Copy all files and build/publish app
COPY . ./
RUN dotnet publish -c Release -o /app/publish

# Use runtime image for the final container
FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app

# Copy the published app from the build stage
COPY --from=build /app/publish .

# Run the app
ENTRYPOINT ["dotnet", "MyS3App.dll"]
