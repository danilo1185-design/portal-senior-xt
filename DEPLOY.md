# Deploy — Render (backend) + Firebase Hosting (frontend)

Arquitetura em produção (tudo grátis, sem cartão):

- **Frontend** (React) → **Firebase Hosting**, site `portalrefrio` → **https://portalrefrio.web.app**
- **Backend** (.NET) → **Render** (serviço `portal-senior-api`, plano free, via Docker)
- O frontend chama a **URL absoluta** do backend no Render (via `VITE_API_BASE` no build). O CORS do backend já libera `portalrefrio.web.app`.

> No PowerShell, deixe o node no PATH da sessão: `$env:Path = "C:\Users\Danilo Lima\AppData\Local\node;$env:Path"`

---

## Estado atual
- ✅ Frontend publicado em https://portalrefrio.web.app.
- ✅ Código preparado para o Render: `render.yaml`, `Dockerfile`, `VITE_API_BASE`, CORS.
- ⏳ Falta subir o backend no Render (precisa de Git + GitHub) e religar o frontend na URL dele.

---

## 1. Instalar o Git
```powershell
winget install Git.Git
```
Feche e reabra o terminal depois (ou instale por https://git-scm.com/download/win).

## 2. Criar repositório no GitHub (PRIVADO)
- Crie uma conta grátis em https://github.com (sem cartão).
- Crie um repositório **privado** (ex.: `portal-senior-xt`). Privado porque o `appsettings.json` tem o IP e a sigla do ERP.

## 3. Subir o código (na raiz do projeto)
```powershell
git init
git add -A
git -c user.name="Danilo Lima" -c user.email="danilo1185@gmail.com" commit -m "Portal Senior XT"
git branch -M main
git remote add origin https://github.com/SEU_USUARIO/portal-senior-xt.git
git push -u origin main
```
> No `push`, o Git vai pedir login do GitHub (abre o navegador / Git Credential Manager).

## 4. Deploy do backend no Render
1. Crie conta grátis (sem cartão) em https://render.com — dá para logar com o GitHub.
2. **New + → Blueprint** → selecione o repositório. O Render lê o `render.yaml` e cria o serviço `portal-senior-api`.
3. Em **Environment**, defina a variável secreta **`Jwt__SecretKey`** com uma chave forte (32+ caracteres). Gere uma:
   ```powershell
   -join ((48..57)+(65..90)+(97..122) | Get-Random -Count 48 | ForEach-Object {[char]$_})
   ```
4. **Apply / Create** e aguarde o build (uns minutos). No fim, copie a **URL do serviço** (ex.: `https://portal-senior-api.onrender.com`).

## 5. Religar o frontend na URL do Render (na raiz do projeto)
```powershell
cd frontend
$env:VITE_API_BASE = "https://SEU-SERVICO.onrender.com/api"   # troque pela URL do passo 4 + /api
npm run build
cd ..
firebase deploy --only hosting --project gruporefrio-14df5
```
Pronto: `https://portalrefrio.web.app` passa a fazer login e consultar vendas.

---

## ⚠️ Atenção
1. **ERP precisa aceitar conexão do Render.** O backend acessa `http://45.236.79.210:8080` de um servidor nos EUA. Se o ERP tiver firewall por IP, é preciso liberar os IPs do Render (o Render tem IPs de saída fixos por serviço — veja em Settings → Outbound). Se o ERP estiver aberto na internet, funciona direto.
2. **Free "dorme".** O serviço free do Render hiberna após ~15 min sem uso; a 1ª chamada depois disso demora ~40s (cold start).
3. **Chave JWT forte** e repositório **privado** (o login autentica no seu Senior).
4. **Performance:** o relatório é lento (~1 min/100 notas pela paginação do WS) — otimizável com blocos em paralelo depois.
