# MCP KSeF Server

To jest serwer MCP dla KSeF - Krajowy System e-Faktur to platforma do wystawiania, przesyłania, otrzymywania i przechowywania faktur.

## Instalacja

[![Install in VS Code](https://img.shields.io/badge/VS_Code-Install-0098FF?style=flat-square&logo=visualstudiocode&logoColor=white)](https://vscode.dev/redirect?url=vscode%3Amcp%2Finstall%3F%7B%22name%22%3A%22markdown-to-html%22%2C%22gallery%22%3Afalse%2C%22command%22%3A%22docker%22%2C%22args%22%3A%5B%22run%22%2C%22-i%22%2C%22--rm%22%2C%22ghcr.io%2Fmicrosoft%2Fmcp-dotnet-samples%2Fmarkdown-to-html%3Alatest%22%5D%7D) [![Install in VS Code Insiders](https://img.shields.io/badge/VS_Code_Insiders-Install-24bfa5?style=flat-square&logo=visualstudiocode&logoColor=white)](https://insiders.vscode.dev/redirect?url=vscode-insiders%3Amcp%2Finstall%3F%7B%22name%22%3A%22markdown-to-html%22%2C%22gallery%22%3Afalse%2C%22command%22%3A%22docker%22%2C%22args%22%3A%5B%22run%22%2C%22-i%22%2C%22--rm%22%2C%22ghcr.io%2Fmicrosoft%2Fmcp-dotnet-samples%2Fmarkdown-to-html%3Alatest%22%5D%7D) [![Install in Visual Studio](https://img.shields.io/badge/Visual_Studio-Install-C16FDE?logo=visualstudio&logoColor=white)](https://aka.ms/vs/mcp-install?%7B%22name%22%3A%22markdown-to-html%22%2C%22gallery%22%3Afalse%2C%22command%22%3A%22docker%22%2C%22args%22%3A%5B%22run%22%2C%22-i%22%2C%22--rm%22%2C%22ghcr.io%2Fmicrosoft%2Fmcp-dotnet-samples%2Fmarkdown-to-html%3Alatest%22%5D%7D)

## Wymagania

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Visual Studio Code](https://code.visualstudio.com/) with
  - [C# Dev Kit](https://marketplace.visualstudio.com/items/?itemName=ms-dotnettools.csdevkit) extension
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)
- [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)
- [Docker Desktop](https://docs.docker.com/get-started/get-docker/)

## Co zawiera

Markdown to HTML MCP server includes:

| Building Block | Name                     | Description                                     | Usage                       |
|----------------|--------------------------|-------------------------------------------------|-----------------------------|
| Tools          | `get_invoice_by_ksef`    | Pobranie faktury w XML na podstawie numeru KSeF | `#get_invoice_by_ksef` |

## Jak to użyć

### Uruchamianie serwera MCP

#### Na maszynie lokalnej

1. Uruchomienie serwera MCP.

    ```bash
    cd $REPOSITORY_ROOT/markdown-to-html
    dotnet run --project ./Mcp-Ksef.HybridApp
    ```
   **Parametry**:

   - `--http`: Przełącznik wskazujący, że serwer MCP ma działać jako serwer strumieniowy HTTP. Po dodaniu tego przełącznika adres URL serwera MCP będzie następujący: `http://localhost:5280`.
   - `--use-ksef-production`: Przełącznik wskazujący czy należy użyć serwera produkcyjnego, domyślnie bez przełącznika MCP użyje serwera testowego.

   Z tymi parametrami możesz użyć serwera MCP jak:

   ```bash
   dotnet run --project ./Mcp-Ksef.HybridApp -- --http --use-ksef-production
   ```

#### In a container

1. Build the MCP server app as a container image.

    ```bash
    cd $REPOSITORY_ROOT
    docker build -f Dockerfile -t mcp-ksef:latest .
    ```

1. Run the MCP server app in a container.

    ```bash
    docker run -i --rm -p 8080:8080 markdown-to-html:latest
    ```

   Alternatively, use the container image from the container registry.

    ```bash
    docker run -i --rm -p 8080:8080 ghcr.io/microsoft/mcp-dotnet-samples/markdown-to-html:latest
    ```

   **Parameters**:

   - `--http`: The switch that indicates to run this MCP server as a streamable HTTP type. When this switch is added, the MCP server URL is `http://localhost:8080`.
   - `--tech-community`/`-tc`: The switch that indicates to convert the markdown text to HTML specific to Microsoft Tech Community.
   - `--extra-paragraph`/`-p`: The switch that indicates whether to put extra paragraph between the given HTML elements that is defined by the `--tags` argument.
   - `--tags`: The comma delimited list of HTML tags that adds extra paragraph in between. Default value is `p,blockquote,h1,h2,h3,h4,h5,h6,ol,ul,dl`

   With these parameters, you can run the MCP server like:

   ```bash
   # use local container image
   docker run -i --rm -p 8080:8080 markdown-to-html:latest --http -tc -p --tags "p,h1,h2,h3,ol,ul,dl"
   ```

   ```bash
   # use container image from the container registry
   docker run -i --rm -p 8080:8080 ghcr.io/microsoft/mcp-dotnet-samples/markdown-to-html:latest --http -tc -p --tags "p,h1,h2,h3,ol,ul,dl"
   ```

#### On Azure

1. Navigate to the directory.

    ```bash
    cd $REPOSITORY_ROOT/markdown-to-html
    ```

1. Login to Azure.

    ```bash
    # Login with Azure Developer CLI
    azd auth login
    ```

1. Deploy the MCP server app to Azure.

    ```bash
    azd up
    ```

   While provisioning and deploying, you'll be asked to provide subscription ID, location, environment name.

1. After the deployment is complete, get the information by running the following commands:

   - Azure Container Apps FQDN:

     ```bash
     azd env get-value AZURE_RESOURCE_MCP_MD2HTML_FQDN
     ```

### Connect MCP server to an MCP host/client

#### VS Code + Agent Mode + Local MCP server

1. Copy `mcp.json` to the repository root.

   **For locally running MCP server (STDIO):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/markdown-to-html/.vscode/mcp.stdio.local.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/markdown-to-html/.vscode/mcp.stdio.local.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   **For locally running MCP server (HTTP):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/markdown-to-html/.vscode/mcp.http.local.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/markdown-to-html/.vscode/mcp.http.local.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   **For locally running MCP server in a container (STDIO):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/markdown-to-html/.vscode/mcp.stdio.container.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/markdown-to-html/.vscode/mcp.stdio.container.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   **For locally running MCP server in a container (HTTP):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/markdown-to-html/.vscode/mcp.http.container.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/markdown-to-html/.vscode/mcp.http.container.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

   **For remotely running MCP server in a container (HTTP):**

    ```bash
    mkdir -p $REPOSITORY_ROOT/.vscode
    cp $REPOSITORY_ROOT/markdown-to-html/.vscode/mcp.http.remote.json \
       $REPOSITORY_ROOT/.vscode/mcp.json
    ```

    ```powershell
    New-Item -Type Directory -Path $REPOSITORY_ROOT/.vscode -Force
    Copy-Item -Path $REPOSITORY_ROOT/markdown-to-html/.vscode/mcp.http.remote.json `
              -Destination $REPOSITORY_ROOT/.vscode/mcp.json -Force
    ```

1. Open Command Palette by typing `F1` or `Ctrl`+`Shift`+`P` on Windows or `Cmd`+`Shift`+`P` on Mac OS, and search `MCP: List Servers`.
1. Choose `markdown-to-html` then click `Start Server`.
1. When prompted, enter one of the following values:
   - The absolute directory path of the `McpSamples.MarkdownToHtml.HybridApp` project
   - The FQDN of Azure Container Apps.
1. Enter prompt like:

    ```text
    Convert the highlighted markdown text to HTML and save it to `converted.html` at the repository root. DO NOT alter the converted HTML.
    ```

1. Confirm the result.
