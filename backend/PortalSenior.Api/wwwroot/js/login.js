(() => {
  "use strict";

  const TOKEN_KEY = "portal-senior.token";

  const form = document.getElementById("login-form");
  const usernameInput = document.getElementById("username");
  const passwordInput = document.getElementById("password");
  const submitButton = document.getElementById("submit");
  const alertBox = document.getElementById("alert");
  const checkErpButton = document.getElementById("check-erp");
  const erpStatus = document.getElementById("erp-status");
  const loginCard = form.closest(".card");
  const welcomeCard = document.getElementById("welcome");
  const welcomeUser = document.getElementById("welcome-user");
  const welcomeExpires = document.getElementById("welcome-expires");
  const logoutButton = document.getElementById("logout");

  function showAlert(message, variant) {
    alertBox.textContent = message;
    alertBox.classList.toggle("alert--warn", variant === "warn");
    alertBox.hidden = false;
  }

  function clearAlert() {
    alertBox.hidden = true;
    alertBox.textContent = "";
  }

  function setBusy(isBusy) {
    submitButton.disabled = isBusy;
    submitButton.classList.toggle("is-loading", isBusy);
    usernameInput.disabled = isBusy;
    passwordInput.disabled = isBusy;
    submitButton.querySelector(".btn__label").textContent = isBusy ? "Entrando..." : "Entrar";
  }

  function showWelcome(username, expiresAtUtc) {
    welcomeUser.textContent = username;
    welcomeExpires.textContent = new Date(expiresAtUtc).toLocaleString("pt-BR");
    loginCard.hidden = true;
    welcomeCard.hidden = false;
  }

  function showLogin() {
    welcomeCard.hidden = true;
    loginCard.hidden = false;
    passwordInput.value = "";
    clearAlert();
    usernameInput.focus();
  }

  async function readMessage(response, fallback) {
    try {
      const data = await response.json();
      return data.message || fallback;
    } catch {
      return fallback;
    }
  }

  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    clearAlert();

    const username = usernameInput.value.trim();
    const password = passwordInput.value;

    if (!username || !password) {
      showAlert("Informe usuário e senha.");
      return;
    }

    setBusy(true);

    try {
      const response = await fetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ username, password }),
      });

      if (response.ok) {
        const data = await response.json();
        sessionStorage.setItem(TOKEN_KEY, data.token);
        showWelcome(data.username, data.expiresAtUtc);
        return;
      }

      // 503 = ERP fora do ar; 401 = credencial inválida. São problemas bem diferentes.
      if (response.status === 503) {
        showAlert(await readMessage(response, "ERP Senior indisponível no momento."), "warn");
        return;
      }

      if (response.status === 401) {
        showAlert(await readMessage(response, "Usuário ou senha inválidos."));
        return;
      }

      showAlert(await readMessage(response, `Erro inesperado (HTTP ${response.status}).`));
    } catch {
      showAlert("Não foi possível contatar o portal. Verifique se o serviço está no ar.");
    } finally {
      setBusy(false);
    }
  });

  checkErpButton.addEventListener("click", async () => {
    checkErpButton.disabled = true;
    erpStatus.hidden = false;
    erpStatus.className = "erp-status";
    erpStatus.textContent = "Testando conexão com o ERP...";

    try {
      const response = await fetch("/api/health/senior");
      const data = await response.json();

      if (data.reachable) {
        erpStatus.classList.add("erp-status--ok");
        erpStatus.textContent = `ERP alcançável em ${data.url} (HTTP ${data.httpStatus}, ${data.elapsedMs} ms).`;
      } else {
        erpStatus.classList.add("erp-status--fail");
        erpStatus.textContent = `ERP inalcançável em ${data.url}. ${data.error || ""} ${data.hint || ""}`.trim();
      }
    } catch {
      erpStatus.classList.add("erp-status--fail");
      erpStatus.textContent = "Não foi possível executar o teste de conexão.";
    } finally {
      checkErpButton.disabled = false;
    }
  });

  logoutButton.addEventListener("click", async () => {
    const token = sessionStorage.getItem(TOKEN_KEY);
    sessionStorage.removeItem(TOKEN_KEY);

    if (token) {
      try {
        await fetch("/api/auth/logout", {
          method: "POST",
          headers: { Authorization: `Bearer ${token}` },
        });
      } catch {
        // Sessão já foi descartada no cliente; falha no servidor não impede o logout.
      }
    }

    showLogin();
  });
})();
