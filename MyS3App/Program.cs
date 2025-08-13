using Amazon.S3;
using Amazon.S3.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

var builder = WebApplication.CreateBuilder(args);

// Add Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Enable Swagger UI
app.UseSwagger();
app.UseSwaggerUI();

// Redirect root "/" to Swagger UI
app.MapGet("/", () => Results.Redirect("/swagger/index.html"));

// ----------- S3 ENDPOINTS -----------

app.MapGet("/s3/buckets", async () =>
{
    using var s3Client = new AmazonS3Client();
    var response = await s3Client.ListBucketsAsync();
    return Results.Ok(response.Buckets.Select(b => b.BucketName));
}).WithName("GetBuckets").WithTags("Buckets");

app.MapPost("/s3/buckets/{bucketName}", async (string bucketName) =>
{
    using var s3Client = new AmazonS3Client();
    var putResponse = await s3Client.PutBucketAsync(bucketName);
    return putResponse.HttpStatusCode == System.Net.HttpStatusCode.OK
        ? Results.Created($"/s3/buckets/{bucketName}", bucketName)
        : Results.BadRequest("Failed to create bucket");
}).WithName("CreateBucket").WithTags("Buckets");

app.MapDelete("/s3/buckets/{bucketName}", async (string bucketName) =>
{
    using var s3Client = new AmazonS3Client();
    var deleteResponse = await s3Client.DeleteBucketAsync(bucketName);
    return deleteResponse.HttpStatusCode == System.Net.HttpStatusCode.NoContent
        ? Results.Ok($"Bucket {bucketName} deleted")
        : Results.BadRequest("Failed to delete bucket");
}).WithName("DeleteBucket").WithTags("Buckets");

app.MapGet("/s3/objects/{bucketName}", async (string bucketName) =>
{
    using var s3Client = new AmazonS3Client();
    var request = new ListObjectsV2Request { BucketName = bucketName, MaxKeys = 100 };
    var response = await s3Client.ListObjectsV2Async(request);
    return Results.Ok(response.S3Objects.Select(o => o.Key));
}).WithName("GetObjects").WithTags("Objects");

app.MapPut("/s3/objects/{bucketName}/{objectKey}", async (string bucketName, string objectKey, HttpRequest request) =>
{
    using var s3Client = new AmazonS3Client();
    using var stream = new MemoryStream();
    await request.Body.CopyToAsync(stream);
    stream.Position = 0;
    var putRequest = new PutObjectRequest { BucketName = bucketName, Key = objectKey, InputStream = stream };
    var response = await s3Client.PutObjectAsync(putRequest);
    return response.HttpStatusCode == System.Net.HttpStatusCode.OK
        ? Results.Ok($"Object {objectKey} uploaded/updated in {bucketName}")
        : Results.BadRequest("Failed to upload/update object");
}).WithName("PutObject").WithTags("Objects");

app.MapDelete("/s3/objects/{bucketName}/{objectKey}", async (string bucketName, string objectKey) =>
{
    using var s3Client = new AmazonS3Client();
    var response = await s3Client.DeleteObjectAsync(bucketName, objectKey);
    return response.HttpStatusCode == System.Net.HttpStatusCode.NoContent
        ? Results.Ok($"Object {objectKey} deleted from {bucketName}")
        : Results.BadRequest("Failed to delete object");
}).WithName("DeleteObject").WithTags("Objects");

// ----------- DYNAMODB ENDPOINTS -----------

// Read all items
app.MapGet("/dynamodb/items", async () =>
{
    var tableName = "MyTable";
    using var client = new AmazonDynamoDBClient();

    var scanRequest = new ScanRequest { TableName = tableName };
    var result = await client.ScanAsync(scanRequest);
    return Results.Ok(result.Items);
}).WithName("GetDynamoItems").WithTags("DynamoDB");

// Create or update item (expects JSON body with "id" key)
app.MapPost("/dynamodb/items", async (HttpRequest request) =>
{
    var tableName = "MyTable";
    using var client = new AmazonDynamoDBClient();

    var json = await new StreamReader(request.Body).ReadToEndAsync();
    var item = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, AttributeValue>>(json);

    if (!item.ContainsKey("id"))
        return Results.BadRequest("Item must contain 'id' as a key");

    var putRequest = new PutItemRequest { TableName = tableName, Item = item };
    await client.PutItemAsync(putRequest);

    return Results.Ok("Item saved to DynamoDB.");
}).WithName("PutDynamoItem").WithTags("DynamoDB");

// Delete item by partition key
app.MapDelete("/dynamodb/items/{id}", async (string id) =>
{
    var tableName = "MyTable";
    using var client = new AmazonDynamoDBClient();

    var deleteRequest = new DeleteItemRequest
    {
        TableName = tableName,
        Key = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = id } } }
    };

    await client.DeleteItemAsync(deleteRequest);
    return Results.Ok($"Item with id={id} deleted.");
}).WithName("DeleteDynamoItem").WithTags("DynamoDB");

app.Run();
