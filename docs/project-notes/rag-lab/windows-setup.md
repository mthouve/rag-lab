# Setup on this Windows laptop

Setup performed on 2026-10-10 for an Intel Core Ultra 7 155H, 32 GB RAM, RTX 4070 Laptop GPU with 8 GB VRAM, and Windows 11 Pro.

## Installed and verified

- Ollama **0.40.2**, installed per user from its official, signature-verified Windows installer.
- `nomic-embed-text:latest`: approximately 274 MB downloaded weights.
- `llama3.2:3b`: approximately 2.0 GB downloaded weights, Q4_K_M quantization.
- Docker Desktop **4.94.0**, installed per user using the WSL 2 backend; its official installer signature was verified.
- Docker CLI **29.8.2** and Compose **5.5.1** were verified. Compose configuration passed validation.
- .NET **10.0.401** SDK permanently installed alongside the existing .NET 9 SDKs. The Microsoft installer succeeded; all three SDKs are present.

The real Ollama test passed: 768-dimensional embeddings, a cited supported answer, and refusal of an unsupported revenue question. `ollama ps` reported **100% GPU** for both models. The chat model used an 8192-token context; NVIDIA reported about **4141 MiB** total GPU memory in use including other desktop activity. This configuration fits your GPU with useful headroom. Model/context choices matter more than the Ollama executable version for VRAM use.

NVIDIA driver **566.14** exceeds Ollama's documented Windows minimum. No GPU driver change was made. The Intel integrated GPU is not needed for these models. Native Windows Ollama avoids routing inference through Docker/WSL; Docker is used for PostgreSQL only.

## Administrator step: WSL 2

**User-confirmed choice (2026-10-10):** `wsl --install --no-distribution`. Ubuntu was intentionally omitted. WSL installation was subsequently verified: WSL 3.0.1.0 is present. Docker engine readiness was subsequently verified after the registration repair below.

For troubleshooting, a missing Ubuntu distribution or an error from `wsl -d Ubuntu` is expected with this choice. Use `wsl --version` to check WSL itself and `wsl --list --verbose` to inspect registered environments. Docker manages its own `docker-desktop` environment. Add Ubuntu explicitly with `wsl --install -d Ubuntu` if a future task requires a personal Linux shell.

Open PowerShell using **Run as administrator**:

```powershell
wsl --install --no-distribution
```

This installs WSL without an extra Ubuntu distribution; Docker supplies its own environment. If Windows requests a restart, save your work and restart manually. No automatic restart has been initiated by this project setup. Afterward, open a new PowerShell window and run:

```powershell
wsl --version
wsl --update
dotnet --version
```

Docker requires WSL 2.1.5 or newer. If Windows reports that virtualization is unavailable, check its specific error before changing BIOS settings. The machine inspection reported an active hypervisor; the other WMI virtualization flags were inconclusive and are not sufficient evidence that BIOS virtualization is disabled.

## Docker's first run

Open **Docker Desktop** from Start. Review and accept its first-run agreement to use it, select WSL 2 / Linux containers, and wait for the engine to start. A Docker account is not needed for the public pgvector image. Sign-in is optional for this local workflow. No paid subscription, cloud resource or account creation was performed.

Check both the client and server:

```powershell
docker version
docker compose version
```

If a terminal predates installation and cannot find the commands, reopen it. Explicit executable paths are:

```powershell
& "$env:LOCALAPPDATA\Programs\DockerDesktop\resources\bin\docker.exe" version
& "$env:LOCALAPPDATA\Programs\Ollama\ollama.exe" --version
```

## Permanent .NET SDK if approval did not complete

The project targets .NET 10; .NET 9 cannot build it. If `dotnet --version` from the repository fails because no compatible SDK is installed, run this in an administrator PowerShell and approve the Microsoft installer:

```powershell
winget install --id Microsoft.DotNet.SDK.10 --exact --source winget
```

Keep the existing .NET 9 SDKs; this is a side-by-side installation.

## Continue acceptance testing

Follow [README startup instructions](../../../README.md#start-on-this-configured-laptop) to choose a local database password, start PostgreSQL, and run the API. In the terminal containing the connection-string environment variable:

```powershell
$env:RAGLAB_TEST_POSTGRES = $env:ConnectionStrings__Postgres
$env:RAGLAB_TEST_OLLAMA = '1'
dotnet test RagLab.sln
```

Then start the API and run `scripts/Verify-Stage1.ps1` from another terminal. PostgreSQL startup, all 31 tests, and the full HTTP smoke script subsequently passed. See the verification record.

Sources: [Ollama Windows requirements](https://docs.ollama.com/windows), [Ollama GPU support](https://docs.ollama.com/gpu), [Llama 3.2 model details](https://ollama.com/library/llama3.2:3b), [Docker Windows installation](https://docs.docker.com/desktop/setup/install/windows-install/), [Microsoft WSL installation](https://learn.microsoft.com/windows/wsl/install).

## Docker launcher registry repair (2026-10-10)

After WSL installation, the Docker launcher reported that `SOFTWARE\Docker Inc.\Docker Desktop` could not be found. Both HKCU/HKLM launcher keys were absent, while the signed binaries and the per-user uninstall record were present. Rerunning the official installer returned exit code 3 because the installed version was already current; it did not restore the key.

A localized repair created `HKCU\Software\Docker Inc.\Docker Desktop` with one string value, `InstallLocation`, set to the existing verified installation directory, `C:\Users\mthou\AppData\Local\Programs\DockerDesktop`. No preexisting key/value was overwritten. The launcher then successfully started the backend and frontend processes. This is a manually restored registration entry, not a vendor-confirmed installer fix. No Docker data or sign-in configuration was removed. Related report: [Docker issue 14912](https://github.com/docker/for-win/issues/14912).

Ubuntu remains intentionally absent; the launcher registry error was separate from distribution selection. After this repair, the Docker engine responded as 29.8.2, PostgreSQL became healthy, and all Stage 1 tests and the HTTP smoke script passed.
