# Diagnóstico operacional

Implementado em 28/10/2026 para distinguir disponibilidade da API e acesso ao banco após a inicialização.

| Endpoint GET | O que verifica | Resposta |
| --- | --- | --- |
| `/api/health` | A API responde, sem acessar o banco; contrato anterior preservado. | HTTP 200, `status: "ok"` e `application: "InvestmentTracker.Api"`. |
| `/api/health/live` | A mesma verificação simples da API. | Mesmo contrato de `/api/health`. |
| `/api/health/ready` | Conectividade com o banco configurado. | HTTP 200 quando disponível; HTTP 503 em falha ou timeout. Inclui `checks.database` com `"ok"` ou `"unavailable"`. |

As respostas usam `Cache-Control: no-store` e não expõem exceções nem detalhes da conexão. A verificação do banco usa o mecanismo nativo de health checks do ASP.NET Core, com um contexto de banco por execução e timeout cooperativo de cinco segundos. O cancelamento depende da cooperação do provedor SQL; esse valor não é garantia rígida de duração total da requisição.

Não há consultas a posições, alterações de dados, migrations ou chamadas aos provedores de câmbio/cotações nessa verificação. Conectar ao banco não comprova integridade dos dados, permissões para todas as operações nem compatibilidade completa do schema.

## Como consultar

Com a API iniciada pelo perfil HTTPS e certificado local confiável:

```powershell
Invoke-RestMethod https://localhost:7097/api/health/live
Invoke-RestMethod https://localhost:7097/api/health/ready
```

Se o banco ficar indisponível depois da inicialização, a verificação simples continua respondendo e a readiness retorna 503. Quando a conexão voltar, a próxima consulta pode retornar 200 sem reiniciar a API. O PowerShell trata o 503 como erro HTTP; o corpo contém os estados dos componentes.

O `DatabaseSchemaGuard` continua verificando modelo e migrations na inicialização. Se a aplicação não conseguir iniciar, nenhum desses endpoints estará disponível. Readiness complementa essa proteção e não aplica migrations automaticamente.

O Playwright E2E agora aguarda `/api/health/ready` para iniciar as jornadas. A consulta é sob demanda: esta entrega não instala monitoramento contínuo, painéis, coleta centralizada de logs, métricas ou traces.

## Validação

Os testes de integração cobrem contrato anterior, disponibilidade, ausência de cache, falha de conexão após startup, recuperação sem reinício e cancelamento por timeout. As falhas são simuladas com interceptores e verificações substitutas, usando somente bancos temporários dos testes.

Validação local desta entrega: 214 testes unitários .NET, 152 testes de integração e 12 cenários E2E aprovados; lint e formatação das configurações aprovados. A base temporária do E2E foi removida ao concluir. As suítes Angular e de apresentação não foram repetidas nesta entrega, que não altera componentes de interface.

Referências: [health checks do ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0) e [limites de CanConnectAsync](https://learn.microsoft.com/pt-br/dotnet/api/microsoft.entityframeworkcore.infrastructure.databasefacade.canconnectasync?view=efcore-10.0).
