## Uruchomienie jako Docker

Wymaga wcześniejsz instalacji [Docker](https://www.docker.com/) na komputerze na którym ma działać serwer MCP.

1. Image Docker jest gotowy do ściągnięcia i instalacji w publiczny repozytorium Docker Hub jako [herbat73/mcp-ksef](https://hub.docker.com/repository/docker/herbat73/mcp-ksef/)

2. Uruchomienie

**Przykładowe komendy**

- system testowy KSeF

```bash
docker run -e KSEF_TOKEN="tutaj twój token ksef" -e KSEF_VATID="tutaj wpisz number nip" -p 8080:8080 herbat73/mcp-ksef:latest --http
```
- system produkcyjny KSeF

```bash
docker run -e KSEF_TOKEN="tutaj twój token ksef" -e KSEF_VATID="tutaj wpisz number nip" -p 8080:8080 herbat73/mcp-ksef:latest --http --use-ksef-production 
```

### Parametry

**KSEF_TOKEN** - wygenerowany token z systemu KSEF dla swojej organizacji

**KSEF_VATID** - numer NIP w formacie bez spacji, prefiksu i znakow formatujących

**Przełączniki**

**--http** - tryb transportowy http

**--use-ksef-production** - tryb produkcujny systemu KsEF (bez przełącznika podłącza się do systemu testowego KSeF)

## Uruchomienie z docker-compose

Przykładowy plik jest tutaj [compose.yaml](../compose.yaml)

```bash
services:
    mcpksef:
    image: herbat73/mcp-ksef:latest
    container_name: mcp-ksef-container
    ports:
      - 8080:8080
    command: --http --use-ksef-production
    environment:
      - KSEF_TOKEN="test my ksef token"
      - KSEF_VATID="my vat id"
```

aby go uruchomić zmienć parametry KSEF_TOKEN oraz KSEF_VATID a następnie wpisz komendę 

```bash
docker-compose -f compose.yaml up -d
```

[Powrót do początku](../README.md)
