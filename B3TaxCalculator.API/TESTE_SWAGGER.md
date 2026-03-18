# 🧪 Guia de Teste - Swagger API

## ✅ Passo 1: Compilar
```bash
cd B3TaxCalculator.API
dotnet build
```

## ✅ Passo 2: Executar
```bash
dotnet run
```

## ✅ Passo 3: Acessar
Abra no navegador:
```
http://localhost:5187/swagger
```

## 🔍 Se não funcionar:

### Erro: "Impossível conectar-se ao servidor"
- A API não está rodando
- Tente: `dotnet run` no terminal

### Erro: "Página em branco"
- Swagger não foi mapeado corretamente
- Verifique se `app.UseSwagger()` está em Program.cs

### Erro: "404 Not Found"
- A porta está errada
- Verifique em `launchSettings.json` qual é a porta correta

## 📝 Arquivos importantes:
- `Program.cs` - Configuração do Swagger
- `launchSettings.json` - Configuração de porta (5187)
- `B3TaxCalculator.API.csproj` - Referências do Swashbuckle

