# Nomic Embed Code Self-Hosting Setup

This document describes how to set up a self-hosted Nomic embedding service for the Azure DevOps PR Review AI project.

## Overview

We've replaced the Claude-based embedding service with `NomicEmbeddingService` that uses the specialized `nomic-embed-code` model, which is specifically trained for code embeddings and achieves state-of-the-art performance on the CodeSearchNet benchmark.

## Benefits of Nomic Embed Code

- **Code-Specific**: Trained specifically on code with function docstrings and respective code
- **Better Performance**: State-of-the-art performance on CodeSearchNet (CSN) benchmark
- **True Embeddings**: Returns proper 768-dimensional embeddings vs Claude's text analysis
- **Cost Effective**: Self-hosted, no per-token charges
- **Open Source**: Fully open source with Apache-2.0 license

## Self-Hosting Options

### Option 1: Using Ollama (Recommended for Development)

1. Install Ollama: https://ollama.com/
2. Pull the nomic-embed-text model (nomic-embed-code not yet available in Ollama):
   ```bash
   ollama pull nomic-embed-text
   ```
3. Run the model:
   ```bash
   ollama serve
   ```
4. Update your appsettings.json:
   ```json
   {
     "NomicEmbedding": {
       "ApiUrl": "http://localhost:11434",
       "Model": "nomic-embed-text",
       "BatchSize": 32,
       "MaxTokens": 8192
     }
   }
   ```

### Option 2: Using Hugging Face Transformers (Python API Server)

1. Create a Python API server using the nomic-embed-code model:

```python
# server.py
from flask import Flask, request, jsonify
from sentence_transformers import SentenceTransformer
import numpy as np

app = Flask(__name__)

# Load the nomic-embed-code model
model = SentenceTransformer("nomic-ai/nomic-embed-code")

@app.route("/v1/embeddings", methods=["POST"])
def generate_embeddings():
    data = request.json
    
    texts = data.get("input", [])
    if isinstance(texts, str):
        texts = [texts]
    
    # Generate embeddings
    embeddings = model.encode(texts, convert_to_tensor=False)
    
    # Format response
    response_data = []
    for i, embedding in enumerate(embeddings):
        response_data.append({
            "embedding": embedding.tolist(),
            "index": i
        })
    
    return jsonify({
        "data": response_data,
        "usage": {
            "total_tokens": sum(len(text.split()) for text in texts)
        }
    })

if __name__ == "__main__":
    app.run(host="0.0.0.0", port=8000)
```

2. Install dependencies:
   ```bash
   pip install flask sentence-transformers torch
   ```

3. Run the server:
   ```bash
   python server.py
   ```

### Option 3: Using Text Generation Inference (Production)

For production deployments, use Hugging Face's Text Generation Inference:

```bash
docker run --gpus all --shm-size 1g -p 8000:80 -v $volume:/data \
    ghcr.io/huggingface/text-embeddings-inference:0.6 \
    --model-id nomic-ai/nomic-embed-code \
    --port 80
```

## Configuration

The `NomicEmbeddingService` has been configured with the following settings:

- **Default API URL**: http://localhost:8000
- **Model**: nomic-ai/nomic-embed-code  
- **Embedding Dimensions**: 768
- **Batch Size**: 32
- **Max Tokens**: 8192

Update your `appsettings.json`:

```json
{
  "NomicEmbedding": {
    "ApiUrl": "http://localhost:8000",
    "Model": "nomic-ai/nomic-embed-code",
    "BatchSize": 32,
    "MaxTokens": 8192
  },
  "VectorDatabase": {
    "VectorSize": 768
  }
}
```

## Testing the Integration

To test the new embedding service:

1. Start your chosen embedding server (Ollama, Python Flask, or TGI)
2. Run the application: `dotnet run`
3. The service will automatically use `NomicEmbeddingService` instead of the Claude-based service

## Performance Comparison

- **Previous (Claude-based)**: Text analysis → Hash-based "embeddings" (1024-dim)
- **New (Nomic-based)**: True neural embeddings optimized for code (768-dim)
- **Expected Improvement**: Better semantic understanding and similarity matching for code

## Troubleshooting

- Ensure the embedding server is running on the configured port
- Check that the API endpoint responds to POST requests at `/v1/embeddings`
- Verify the vector database is configured for 768-dimensional embeddings
- Monitor logs for embedding generation errors

The system will gracefully fall back to empty embeddings if the service is unavailable, allowing the application to continue running without embeddings.