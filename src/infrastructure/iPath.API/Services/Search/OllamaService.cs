using Microsoft.Extensions.AI;
using OllamaSharp;
using OllamaSharp.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace iPath.API.Services.Search;

public class OllamaService
{
    public async Task<ReadOnlyMemory<float>> GenerateEmbedding(string text)
    {
        IEmbeddingGenerator<string, Embedding<float>> generator =
            new OllamaApiClient(new Uri("http://localhost:11434/"), "nomic-embed-text");

        ReadOnlyMemory<float> vector = await generator.GenerateVectorAsync(text);
        return vector;
    }
}