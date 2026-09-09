using System.Text.Json.Serialization;
using MockingBird.Middleware;
using MockingBird.Middleware.Configuration;
using MockingBird.TestApi.Data;
using MockingBird.TestApi.Swagger;

if (File.Exists(".env"))
{
    DotNetEnv.Env.Load();
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton<PeopleStore>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "MockingBird.TestApi",
        Version = "v1",
        Description = "Fixture API demonstrating MockingBird: /v1/people is real, /v1/orders is mock-only.",
    });
    options.DocumentFilter<MockOnlyEndpointsDocumentFilter>();
});

builder.Services.AddMockingBird(options =>
{
    if (Enum.TryParse<MockProviderKind>(builder.Configuration["MockingBird:Provider"], ignoreCase: true, out var provider))
    {
        options.Provider = provider;
    }

    options.OpenRouterApiKey = builder.Configuration["MockingBird:OpenRouterApiKey"];
    options.Model = builder.Configuration["MockingBird:Model"] ?? options.Model;

    options.Bedrock.AccessKeyId = builder.Configuration["MockingBird:Bedrock:AccessKeyId"];
    options.Bedrock.SecretAccessKey = builder.Configuration["MockingBird:Bedrock:SecretAccessKey"];
    options.Bedrock.SessionToken = builder.Configuration["MockingBird:Bedrock:SessionToken"];
    options.Bedrock.Region = builder.Configuration["MockingBird:Bedrock:Region"] ?? options.Bedrock.Region;
    options.Bedrock.ModelId = builder.Configuration["MockingBird:Bedrock:ModelId"] ?? options.Bedrock.ModelId;

    options.DocumentName = "v1";
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "MockingBird.TestApi v1"));

app.UseRouting();

app.UseMockingBird();

app.MapControllers();

app.Run();

// Exposed for WebApplicationFactory<Program> in the test project.
public partial class Program;
