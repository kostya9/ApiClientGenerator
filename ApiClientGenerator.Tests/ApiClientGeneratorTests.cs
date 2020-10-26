using System;
using Xunit;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Collections.Immutable;
using System.Linq;
using Xunit.Abstractions;

namespace ApiClientGenerator.Tests
{
    public class ApiClientGeneratorTests
    {

        [Fact]
        public void CanDetermineControllerBase_ActionWithBody()
        {
            var result = RunGenerator(@"
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
public record A (string AB);

[ApiController]
public class MyApiController : ControllerBase
{
    [HttpPost(""{id}""]
    public async Task<ActionResult<string>> Create(int id, A a)
    {
       return ""a"";
    }
}
");
            var expected = @"
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Json;

public class ApiClient
{
    private readonly HttpClient _client;

    public ApiClient(HttpClient client)
    {
        _client = client;
        MyApi = new MyApi(client);
    }

    public MyApi MyApi { get; }
}

public class MyApi
{
    private readonly HttpClient _client;

    public MyApi(HttpClient client)
    {
        _client = client;
    }

    public async Task<string> Create(int id, A a)
    {
        var result = await _client.PostAsJsonAsync($""{id}"", a);
        return await result.Content.ReadFromJsonAsync<string>();
    }
}
";
            Assert.Equal(expected.Trim(), result.Trim());
        }

        [Fact]
        public void CanDetermineController()
        {
            var result = RunGenerator(@"
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

[ApiController]
public class MyApiController : Controller
{
    [HttpPost(""{id}""]
    public async Task<ActionResult<string>> Create(int id)
    {
       return ""a"";
    }
}
");

            var expected = @"
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Json;

public class ApiClient
{
    private readonly HttpClient _client;

    public ApiClient(HttpClient client)
    {
        _client = client;
        MyApi = new MyApi(client);
    }

    public MyApi MyApi { get; }
}

public class MyApi
{
    private readonly HttpClient _client;

    public MyApi(HttpClient client)
    {
        _client = client;
    }

    public async Task<string> Create(int id)
    {
        var result = await _client.PostAsync($""{id}"");
        return await result.Content.ReadFromJsonAsync<string>();
    }
}
";
            Assert.Equal(expected.Trim(), result.Trim());
        }


        private string RunGenerator(string source)
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);

            List<MetadataReference> references = new List<MetadataReference>();
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                if (!assembly.IsDynamic)
                {
                    references.Add(MetadataReference.CreateFromFile(assembly.Location));
                }
            }

            CSharpCompilation compilation = CSharpCompilation.Create("original", new SyntaxTree[] { syntaxTree }, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            ISourceGenerator generator = new ApiClientGenerator();

            CSharpGeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
            driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outputCompilation, out ImmutableArray<Diagnostic> diagnostics);
            Assert.False(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error), "Failed: " + diagnostics.FirstOrDefault()?.GetMessage());

            var output = outputCompilation.SyntaxTrees.Skip(1).First().ToString();
            return output;
        }
    }
}
