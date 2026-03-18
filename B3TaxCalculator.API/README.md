# B3TaxCalculator API

API REST para cálculo de impostos sobre operações de trading na B3.

## 🚀 Como Usar

### 1. Iniciar a API

```bash
cd B3TaxCalculator.API
dotnet run
```

A API estará disponível em `https://localhost:5001` (HTTPS) ou `http://localhost:5000` (HTTP).

### 2. Acessar a Documentação Swagger

Navegue até: `https://localhost:5001/swagger/index.html`

Você verá a interface interativa do Swagger com todos os endpoints disponíveis.

---

## 📡 Endpoints Disponíveis

### **1. POST /api/taxcalculation/process-pdf**

Processa um único arquivo PDF de nota de corretagem.

**Request:**
```http
POST /api/taxcalculation/process-pdf HTTP/1.1
Content-Type: multipart/form-data

[Arquivo PDF]
```

**Response (Success - 200):**
```json
{
  "success": true,
  "fileName": "XPINC_NOTA_NEGOCIACAO_B3_25_2_2026.pdf",
  "tradesFound": 24,
  "validTrades": 23,
  "monthlyResults": [
    {
      "year": 2026,
      "month": 2,
      "stockTotalBuy": 354.14,
      "stockTotalSell": 0.00,
      "stockTotalFees": 0.16,
      "stockProfit": 0,
      "stockLoss": 0,
      "stockTax": 0,
      "stockIsExempt": true,
      "stockDescription": "Isento - vendas (R$ 0,00) abaixo de R$ 20.000,00",
      "optionTotalBuy": 39.03,
      "optionTotalSell": 77.89,
      "optionTotalFees": 88.28,
      "optionCompensatingBuyTotal": 39.03,
      "optionGrossSell": 77.89,
      "optionProfit": 38.86,
      "optionTax": 5.83,
      "optionDescription": "DARF: R$ 5,83 (15% sobre lucro de R$ 38,86)",
      "optionAuditEntries": [...]
    }
  ]
}
```

**Response (Error - 400/500):**
```json
{
  "success": false,
  "message": "Arquivo PDF é obrigatório",
  "error": null
}
```

---

### **2. POST /api/taxcalculation/process-pdfs**

Processa múltiplos arquivos PDF de uma vez.

**Request:**
```http
POST /api/taxcalculation/process-pdfs HTTP/1.1
Content-Type: multipart/form-data

[Arquivo 1]
[Arquivo 2]
[Arquivo 3]
```

**Response (Success - 200):**
```json
{
  "success": true,
  "filesProcessed": [
    "XPINC_NOTA_NEGOCIACAO_B3_25_2_2026.pdf",
    "XPINC_NOTA_NEGOCIACAO_B3_3_2026.pdf"
  ],
  "totalFilesRequested": 2,
  "totalTradesFound": 48,
  "totalValidTrades": 46,
  "monthlyResults": [...]
}
```

---

### **3. POST /api/taxcalculation/calculate-trades**

Calcula impostos a partir de uma lista JSON de operações (sem necessidade de PDF).

**Request:**
```json
POST /api/taxcalculation/calculate-trades HTTP/1.1
Content-Type: application/json

[
  {
    "date": "2026-02-25",
    "asset": "CMINO540",
    "market": "OPCAO_VENDA",
    "side": "V",
    "quantity": 100,
    "price": 0.14,
    "fees": 0.02,
    "notaNumber": "12345"
  },
  {
    "date": "2026-02-25",
    "asset": "CMINO540",
    "market": "OPCAO_VENDA",
    "side": "C",
    "quantity": 100,
    "price": 0.17,
    "fees": 0.01,
    "notaNumber": "12345"
  }
]
```

**Response (Success - 200):**
```json
{
  "success": true,
  "tradesProcessed": 2,
  "validTrades": 2,
  "monthlyResults": [...]
}
```

---

## 🔧 Exemplos com cURL

### Processar um PDF:
```bash
curl -X POST "https://localhost:5001/api/taxcalculation/process-pdf" \
  -F "file=@nota.pdf"
```

### Processar múltiplos PDFs:
```bash
curl -X POST "https://localhost:5001/api/taxcalculation/process-pdfs" \
  -F "files=@nota1.pdf" \
  -F "files=@nota2.pdf" \
  -F "files=@nota3.pdf"
```

### Calcular operações via JSON:
```bash
curl -X POST "https://localhost:5001/api/taxcalculation/calculate-trades" \
  -H "Content-Type: application/json" \
  -d '[
    {
      "date": "2026-02-25",
      "asset": "CMINO540",
      "market": "OPCAO_VENDA",
      "side": "V",
      "quantity": 100,
      "price": 0.14,
      "fees": 0.02,
      "notaNumber": "12345"
    }
  ]'
```

---

## 🛠️ Exemplos com Postman

1. **Criar nova requisição POST**
2. **URL:** `https://localhost:5001/api/taxcalculation/process-pdf`
3. **Tab "Body":**
   - Selecionar **form-data**
   - Key: `file`
   - Type: **File**
   - Selecionar arquivo PDF
4. **Enviar (Send)**

---

## 📊 Estrutura da Resposta MonthlyResult

```json
{
  "year": 2026,
  "month": 2,

  "priorMonthTaxCarryover": 0.00,
  "taxCarryoverToNextMonth": 5.83,
  "taxToPayThisMonth": 0.00,

  "stockTotalBuy": 354.14,
  "stockTotalSell": 0.00,
  "stockTotalFees": 0.16,
  "stockProfit": 0,
  "stockLoss": 0,
  "stockAccumulatedLoss": 0,
  "stockTaxableProfit": 0,
  "stockTax": 0,
  "stockIsExempt": true,
  "stockDescription": "Isento - vendas (R$ 0,00) abaixo de R$ 20.000,00",

  "optionTotalBuy": 39.03,
  "optionTotalSell": 77.89,
  "optionTotalFees": 88.28,
  "optionCompensatingBuyTotal": 39.03,
  "optionGrossSell": 77.89,
  "optionNetProfit": 32.85,
  "optionProfit": 38.86,
  "optionLoss": 0,
  "optionAccumulatedLoss": 0,
  "optionTaxableProfit": 38.86,
  "optionTax": 5.83,
  "optionDescription": "DARF: R$ 5,83 (15% sobre lucro de R$ 38,86)",
  "optionCompensatingTrades": ["25/02: COMPRA CMINO540 100x @ 0,17 = R$ 39,03"],
  "optionAuditEntries": [
    {
      "date": "2026-02-20",
      "asset": "CMINO540",
      "side": "V",
      "grossValue": 24.00,
      "netValueImpact": 1.97,
      "fees": 22.03,
      "accumulatedNetValue": 1.97,
      "price": 0.24,
      "quantity": 100
    }
  ],

  "totalTax": 5.83
}
```

---

## 🔐 CORS (Cross-Origin Resource Sharing)

A API está configurada com CORS permissivo para desenvolvimento:

```csharp
policy.AllowAnyOrigin()
      .AllowAnyMethod()
      .AllowAnyHeader();
```

**Para produção**, configure CORS restritivo:

```csharp
policy.WithOrigins("https://seudominio.com")
      .AllowAnyMethod()
      .AllowAnyHeader();
```

---

## 🐛 Tratamento de Erros

### Requisição Inválida (400)
```json
{
  "success": false,
  "message": "Arquivo PDF é obrigatório",
  "error": null
}
```

### Erro de Processamento (500)
```json
{
  "success": false,
  "message": "Erro ao processar PDF",
  "error": "Exception details here..."
}
```

---

## 📦 Dependências

- **ASP.NET Core 10.0**
- **Swashbuckle.AspNetCore 6.10.0** (Swagger/OpenAPI)
- **UglyToad.PdfPig** (Parser PDF)

---

## 🚢 Deploy

### Docker (Exemplo)

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 80
ENTRYPOINT ["dotnet", "B3TaxCalculator.API.dll"]
```

### Executar Docker:
```bash
docker build -t b3taxcalculator-api .
docker run -p 8080:80 b3taxcalculator-api
```

---

## 📝 Notas

- Arquivos PDF são carregados em `Path.GetTempPath()` e deletados após processamento
- Máximo de arquivos simultâneos: Limitado pela memória disponível
- Suporta operações de exercício de opção (marcadas com flag `isExercise`)
- Cálculos de impostos: 15% para opções, isenção para ações com vendas < R$ 20.000

---

## 🤝 Contribuindo

Para contribuir, faça fork do repositório e crie uma branch com sua feature.

---

## 📄 Licença

MIT License
