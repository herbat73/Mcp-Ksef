## Punkty (endpoints) dostępu serwera

- /info - endpoint informacyjny, powinien zwrócić jak

`
{"name":"MCP KSeF","version":"1.0.0","transport":"streamable-http","endpoints":{"mcp":"/mcp","health":"/health"},"description":"MCP server for connecting KSeF repository"}
`
- /health - sprawdzanie stanu serewera MCP, powinien zwrócić jak

`
{"status":"healthy","timestamp":"2026-02-02T16:23:48.6937149Z"}
`

- /mcp - właściwy endpoint dla komend MCP 

- /swagger.json - definicja swagger 2.0

- /openapi.json - definicja OpenApi 3.0

[Powrót do początku](../README.md)