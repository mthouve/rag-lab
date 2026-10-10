# Run after starting PostgreSQL, Ollama, and the API. No credentials are embedded here.
[CmdletBinding()]
param([string]$BaseUrl = 'http://127.0.0.1:5080')
$ErrorActionPreference = 'Stop'
function Post-Json([string]$Path, [object]$Body) {
    Invoke-RestMethod -Method Post -Uri "$BaseUrl$Path" -ContentType 'application/json' -Body ($Body | ConvertTo-Json) -TimeoutSec 600
}
$health = Invoke-RestMethod "$BaseUrl/health"
if ($health.status -ne 'ready') { throw 'API health check failed.' }
foreach ($file in @('sample-documents/rag-overview.md', 'sample-documents/postgresql-notes.txt')) {
    $result = Post-Json '/api/documents/index' @{ path = $file }
    if ($result.status -notin @('indexed', 'unchanged')) { throw "Indexing failed for $file" }
    Write-Host "$file : $($result.status)"
    $duplicate = Post-Json '/api/documents/index' @{ path = $file }
    if ($duplicate.status -ne 'unchanged') { throw 'Unchanged document was unnecessarily reindexed.' }
}
$answer = Post-Json '/api/ask' @{ question = 'How does RAG Lab preserve the previous index when an update fails?' }
if ($answer.insufficientContext -or $answer.citations.Count -eq 0) { throw 'Expected a supported answer with citations.' }
if (-not ($answer.citations | Where-Object sourceLocation -eq 'sample-documents/rag-overview.md')) { throw 'Expected a citation to rag-overview.md.' }
$answer | ConvertTo-Json -Depth 8
$unknown = Post-Json '/api/ask' @{ question = 'What was the exact revenue of Project Atlas in 2025?' }
if (-not $unknown.insufficientContext) { throw 'The model answered a question absent from these documents. Review grounding behavior.' }
Write-Host 'Stage 1 HTTP verification passed: indexing, duplicate detection, retrieval, citations, unsupported question.'
