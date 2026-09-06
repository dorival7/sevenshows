# 📑 SNAPSHOT HISTÓRICO DE ENGENHARIA DE SOFTWARE: SEVENSHOWS
> **Objetivo:** Documento de Transição de Estado (State Snapshot) para continuidade em IA.  
> **Escopo:** Área Administrativa e Comercial do Artista (Músico/Tenant).  
> **Arquitetura Front-end:** Vue 3 (Webpack / Porta 8081 / Template Velzon Themesbrand).  
> **Arquitetura Backend:** .NET 10 C# (Porta 5297 / Vertical Slices / MySQL via EF Core).  
> **Músico de Teste Autenticado:** "Banda Dois" (Status no Banco: `Active` / Plano SaaS Pago via Asaas).

---

## 🛠️ 1. MÓDULO DE LOGIN E COMPILAÇÃO GLOBAL (VUE 3)

### 1.1 Solução do Erro de Parser do ESLint (`x-invalid-namespace`)
*   **Problema:** O compilador do Vue CLI Services abortava o build na tela de login acusando falha estática (`vue/no-parsing-error`) devido à presença do atributo de namespace `xmlns="http://w3.org"` embutido diretamente na tag `<svg>` decorativa de fundo do layout.
*   **Solução:** O atributo foi removido do arquivo `login.vue`. Como os navegadores modernos processam inline-SVGs nativamente no HTML5 sem a necessidade desse link de namespace, o design foi preservado e o terminal passou a exibir `Compiled successfully` na porta `8081`.

### 1.2 Restabelecimento do Chassi de Layout Responsivo e Botão Hambúrguer
*   **Problema:** O nó raiz do DOM `<div id="app" data-v-app>` nascia fechado/recolhido e o miolo central ficava inteiramente em branco. O JavaScript de layout do Velzon perdia a referência de cálculo de largura, fazendo o clique no botão hambúrguer mudar o ícone para uma seta, mas sem executar a animação de encolher o menu lateral.
*   **Solução:** 
    1. **Higienização do Chassi:** O arquivo `src/layouts/vertical.vue` foi limpo, removendo referências a elementos e escutadores fantasmas (como o ID `#overlay` herdado da Themesbrand) que disparavam exceções nulas abortando o ciclo de vida do Vue.
    2. **Remoção de Redundâncias de Grid:** As classes CSS `.main-content` e `.page-content` foram removidas do miolo das views filhas e centralizadas exclusivamente no chassi vertical para evitar conflitos de margem.
    3. **Ponto de Encaixe:** Substituição das tags `<slot />` profundas do layout pelo componente oficial de renderização dinâmica do roteador no Velzon Vue 3:
    ```html
    <div class="container-fluid">
        <router-view />
    </div>
    ```

### 1.3 Injeção Reativa de Credenciais Reais no Cabeçalho
*   **Barra Superior (`nav-bar.vue`):** Substituição de todos os textos e nomes de demonstração herdados pela propriedade computada `currentUser.name`, que extrai o nome do músico real (**Banda Dois**) gravado no `LocalStorage` pelo formulário de login real.
*   **Avatar Neutro:** Substituição do arquivo de imagem estática de modelo pelo asset universal `user-dummy-img.jpg` para servir de placeholder elegante enquanto o músico não realiza o upload da foto oficial do portfólio.

---

## 🗄️ 2. DICIONÁRIO DE DADOS E INFRAESTRUTURA MYSQL (EF CORE)

Conforme a Especificação Técnica de Arquitetura do backend, o contexto global do Entity Framework Core (`AppDbContext.cs`) foi expandido para suportar o cálculo real das métricas da Dashboard através de consultas LINQ agregadas, eliminando 100% de dados mockados no C#.

### 2.1 Mapeamento de Modelos Físicos (`src/Modules/Tenants/Models/`)
```csharp
// ArtistEvent.cs (Tabela de Agenda / Compromissos Comerciais)
public class ArtistEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string VenueName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Status { get; set; } = "Confirmed"; // Confirmed, Canceled
}

// ArtistWalletTransaction.cs (Tabela Financeira / Custódia do Gateway Asaas)
public class ArtistWalletTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Type { get; set; } = "Receivable"; // Receivable, Payout
    public decimal Value { get; set; }
    public bool IsReleased { get; set; } = false; // False = Cachê a Receber, True = Sacado
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

### 2.2 Fluent Configuration no `AppDbContext.cs`
Mapeamento de tipos decimais de alta precisão injetado para evitar truncamento de moedas no MySQL Workbench:
```csharp
public DbSet<ArtistPackage> ArtistPackages { get; set; } = default!;
public DbSet<ArtistEvent> ArtistEvents { get; set; } = default!;
public DbSet<ArtistWalletTransaction> ArtistWalletTransactions { get; set; } = default!;

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<ArtistPackage>(entity => {
        entity.Property(p => p.BasePrice).HasColumnType("decimal(18,2)");
    });
    modelBuilder.Entity<ArtistWalletTransaction>(entity => {
        entity.Property(t => t.Value).HasColumnType("decimal(18,2)");
    });
}
```
## 🚦 3. BACKEND: INTEGRAÇÃO DE ENDPOINTS E REGRAS DE NEGÓCIO (`TenantsController.cs`)

Os métodos abaixo foram implementados de raiz no ecossistema .NET 10, acoplados às tabelas do MySQL e aos cabeçalhos de autorização Bearer.

```csharp
    // ====================================================================
    // 4.1. GET: Listar Todos os Pacotes de Shows Cadastrados no MySQL
    // ====================================================================
    [HttpGet("packages")]
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> GetPackages()
    {
        var tenantIdLogado = ObterTenantIdLogado();

        var pacotes = await _context.ArtistPackages
            .Where(p => p.UserId == tenantIdLogado)
            .Select(p => new {
                id = p.Id,
                title = p.Title,
                durationMinutes = p.DurationMinutes,
                basePrice = p.BasePrice,
                description = p.Description ?? ""
            }).ToListAsync();

        return Ok(pacotes);
    }

    // ====================================================================
    // 4.2. PUT: Editar / Atualizar Dados de um Pacote de Show Existente
    // ====================================================================
    [HttpPut("packages/{id}")]
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> UpdatePackage(Guid id, [FromBody] SavePackageRequest model)
    {
        var tenantIdLogado = ObterTenantIdLogado();
        var pacote = await _context.ArtistPackages
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == tenantIdLogado);

        if (pacote == null) return NotFound("Formato de show não localizado.");

        pacote.Title = model.Title;
        pacote.DurationMinutes = model.DurationMinutes;
        pacote.BasePrice = model.BasePrice;
        pacote.Description = model.Description;

        await _context.SaveChangesAsync();
        return Ok(new { message = "Formato de show atualizado com sucesso!" });
    }

    // ====================================================================
    // 4.3. DELETE: Remover um Pacote de Show do Catálogo do MySQL
    // ====================================================================
    [HttpDelete("packages/{id}")]
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> DeletePackage(Guid id)
    {
        var tenantIdLogado = ObterTenantIdLogado();
        var user = await _context.Users.FindAsync(tenantIdLogado);
        if (user == null) return NotFound("Músico não encontrado.");

        var pacote = await _context.ArtistPackages
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == tenantIdLogado);

        if (pacote == null) return NotFound("Formato de show não localizado.");

        _context.ArtistPackages.Remove(pacote);

        int totalPacotesRestantes = await _context.ArtistPackages.CountAsync(p => p.UserId == tenantIdLogado) - 1;
        if (user.ProfileStatus == "Incomplete_Media" && totalPacotesRestantes == 0)
        {
            user.ProfileStatus = "Incomplete_Packages";
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Pacote de show removido com sucesso!" });
    }

    // ====================================================================
    // 8. GET: Métricas e Dados Reais do Banco de Dados para a Dashboard
    // ====================================================================
    [HttpGet("dashboard-metrics")]
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> GetDashboardMetrics()
    {
        var tenantIdLogado = ObterTenantIdLogado();
        var dataAtual = DateTime.UtcNow;

        var user = await _context.Users.Include(u => u.CurrentPlan).FirstOrDefaultAsync(u => u.Id == tenantIdLogado);
        if (user == null) return NotFound("Músico não encontrado.");

        int totalPacotes = await _context.ArtistPackages.CountAsync(p => p.UserId == tenantIdLogado);
        var configComercial = await _context.ArtistComercialSettings.FirstOrDefaultAsync(c => c.UserId == tenantIdLogado);
        int raioKm = configComercial?.FreeRadiusKm ?? 0;

        int showsNoMes = await _context.ArtistEvents.CountAsync(e => 
            e.UserId == tenantIdLogado && e.EventDate.Month == dataAtual.Month && e.EventDate.Year == dataAtual.Year && e.Status == "Confirmed");

        decimal saldoAReceber = await _context.ArtistWalletTransactions
            .Where(t => t.UserId == tenantIdLogado && t.Type == "Receivable" && t.IsReleased == false).SumAsync(t => t.Value);

        // Algoritmo Universal de Progresso Matemático Ponderado ( UX Perfeito)
        int percentualProgresso = 0;
        if (user.ProfileStatus == "Active")
        {
            percentualProgresso = 70; // Base fixa de Onboarding + SaaS quitados
            if (totalPacotes == 1) percentualProgresso += 10;      // 80% (Caso da Banda Dois)
            else if (totalPacotes == 2) percentualProgresso += 20; // 90%
            else if (totalPacotes >= 3) percentualProgresso += 30; // 100% Total
        }
        else
        {
            percentualProgresso = user.ProfileStatus switch {
                "Incomplete_Logistics" => 25,
                "Incomplete_Packages" => 50,
                "Incomplete_Media" => 60,
                "Pending_Payment" => 70,
                _ => 0
            };
        }

        var eventosFuturos = await _context.ArtistEvents
            .Where(e => e.UserId == tenantIdLogado && e.EventDate >= dataAtual && e.Status == "Confirmed")
            .OrderBy(e => e.EventDate).Take(3)
            .Select(e => new {
                Data = e.EventDate.ToString("dd"),
                MesAno = e.EventDate.ToString("MMMM, yyyy"),
                NomeEvento = e.Title,
                Local = e.VenueName,
                CidadeEstado = \$"{e.City} - {e.State}"
            }).ToListAsync();

        return Ok(new {
            showsEsteMes = showsNoMes,         
            cachesAReceber = saldoAReceber, 
            pacotesCriados = totalPacotes,
            totalPacotesPermitidos = 3,
            raioDeslocamentoKm = raioKm,
            progressoEpkPercentual = percentualProgresso,
            proximosShows = eventosFuturos
        });
    }
```

---

## 🛡️ 4. FRONT-END: INTERCEPTADOR GLOBAL DE SEGURANÇA CORRIGIDO (`src/router/index.js`)

O arquivo central de rotas do Vue 3 foi limpo de qualquer dado simulado antigo e estruturado para validar as travas reais por status vindas da API.

```javascript
import { createWebHistory, createRouter } from "vue-router";
import axios from 'axios';
import musicosRoutes from "../musicos/routes";

const router = createRouter({
  history: createWebHistory(),
  routes: [
    ...musicosRoutes,
    { path: "/", name: "login-root", component: () => import("../views/account/login.vue") },
    { path: "/login", name: "login", component: () => import("../views/account/login.vue") }
  ]
});

router.beforeEach(async (routeTo, routeFrom, next) => {
  const authRequired = routeTo.matched.some((route) => route.meta?.authRequired);
  if (!authRequired) return next();

  const token = localStorage.getItem('jwt');
  const profileStatus = localStorage.getItem('profileStatus');

  if (!token) {
    return next({ name: 'login', query: { redirectFrom: routeTo.fullPath } });
  }

  axios.defaults.headers.common['authorization'] = 'Bearer ' + token;

  // ESTEIRA DE ONBOARDING AUTOMATIZADA
  if (profileStatus === "Incomplete_Logistics") {
    if (routeTo.path !== "/musicos/logistica") return next("/musicos/logistica");
    return next();
  }
  if (profileStatus === "Incomplete_Packages") {
    if (routeTo.path !== "/musicos/pacotes") return next("/musicos/pacotes");
    return next();
  }
  if (profileStatus === "Incomplete_Media") {
    if (routeTo.path !== "/musicos/portfolio") return next("/musicos/portfolio");
    return next();
  }
  if (profileStatus === "Pending_Payment") {
    if (routeTo.path !== "/musicos/assinatura") return next("/musicos/assinatura");
    return next();
  }

  next();
});

export default router;
```
## 🎨 5. FRONT-END: CÓDIGOS INTERATIVOS E UNIFICADOS (ÁREA DO ARTISTA - SUBPARTE A)

### 5.1 Central de Logística Comercial (`src/musicos/views/Logistica.vue`)
Formulário completo com carregamento dinâmico (`GET`) e atualização por escrita (`POST`) contra o C#.

```vue
<template>
  <div>
    <div class="row">
      <div class="col-12">
        <div class="page-title-box d-sm-flex align-items-center justify-content-between">
          <h4 class="mb-sm-0 text-primary">Configurações de Logística e Frete</h4>
        </div>
      </div>
    </div>

    <div v-if="successMessage" class="alert alert-success border-0 shadow-sm">{{ successMessage }}</div>
    <div v-if="errorMessage" class="alert alert-danger border-0 shadow-sm">{{ errorMessage }}</div>

    <div v-if="loading" class="text-center py-5">
      <div class="spinner-border text-primary avatar-sm"></div>
    </div>

    <form v-else @submit.prevent="saveSettings">
      <div class="row">
        <div class="col-xl-8">
          <div class="card">
            <div class="card-body">
              <div class="row">
                <div class="col-md-6 mb-3">
                  <label class="form-label fw-semibold">Raio de Cobertura Gratuita (KM)</label>
                  <div class="input-group">
                    <input type="number" class="form-control" v-model.number="form.freeRadiusKm" required min="0" />
                    <span class="input-group-text">KM</span>
                  </div>
                </div>
                <div class="col-md-6 mb-3">
                  <label class="form-label fw-semibold">Valor do KM Extra Adicional</label>
                  <div class="input-group">
                    <span class="input-group-text">R\$</span>
                    <input type="number" class="form-control" step="0.01" v-model.number="form.extraKmValue" required min="0" />
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div class="card">
            <div class="card-body">
              <div class="form-check form-switch form-switch-md mb-4">
                <input class="form-check-input" type="checkbox" id="extraHours" v-model="form.acceptExtraHours" />
                <label class="form-check-label fw-medium text-primary" for="extraHours">
                  {{ form.acceptExtraHours ? "Sim, aceitamos realizar horas extras" : "Não fazemos horas extras" }}
                </label>
              </div>
              <div class="row" v-if="form.acceptExtraHours">
                <div class="col-md-6 mb-3">
                  <label class="form-label fw-semibold">Preço por Hora Adicional</label>
                  <div class="input-group">
                    <span class="input-group-text">R\$</span>
                    <input type="number" class="form-control" step="0.01" v-model.number="form.extraHourValue" required min="0" />
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div class="col-xl-4">
          <div class="card">
            <div class="card-body">
              <label class="form-label fw-semibold">Regiões Atendidas</label>
              <textarea class="form-control" rows="4" v-model="form.attendedRegions" required></textarea>
            </div>
          </div>
          <button type="submit" class="btn btn-success btn-lg w-100 shadow" :disabled="saving">
            {{ saving ? "Gravando no MySQL..." : "Salvar Configurações Comerciais" }}
          </button>
        </div>
      </div>
    </form>
  </div>
</template>

<script>
import axios from 'axios';
export default {
  name: "MusicoLogistica",
  data() {
    return {
      loading: true, saving: false, successMessage: null, errorMessage: null,
      form: { freeRadiusKm: 0, extraKmValue: 0.00, acceptExtraHours: false, extraHourValue: 0.00, attendedRegions: "" }
    };
  },
  methods: {
    async loadLogisticsSettings() {
      this.loading = true;
      try {
        const token = localStorage.getItem('jwt');
        const config = { headers: { Authorization: `Bearer ${token}` } };
        const response = await axios.get(`${process.env.VUE_APP_API_BASE_URL}/tenants/commercial-settings`, config);
        this.form = response.data;
        this.loading = false;
      } catch (error) {
        this.loading = false;
        this.errorMessage = "Erro ao buscar dados.";
      }
    },
    async saveSettings() {
      this.saving = true;
      try {
        const token = localStorage.getItem('jwt');
        const config = { headers: { Authorization: `Bearer ${token}` } };
        await axios.post(`${process.env.VUE_APP_API_BASE_URL}/tenants/commercial-settings`, this.form, config);
        this.successMessage = "Configurações updated!";
        this.saving = false;
        if (localStorage.getItem('profileStatus') === 'Incomplete_Logistics') {
          localStorage.setItem('profileStatus', 'Incomplete_Packages');
        }
      } catch (error) {
        this.saving = false;
        this.errorMessage = "Erro ao salvar.";
      }
    }
  },
  mounted() { this.loadLogisticsSettings(); }
};
</script>
```
### 5.2 Catálogo Comercial de Pacotes com Máscara (`src/musicos/views/Pacotes.vue`)
Gerenciador completo (CRUD) integrado a `GET`, `POST`, `PUT` e `DELETE` no .NET 10.

```vue
<template>
  <div>
    <div class="row">
      <div class="col-12">
        <div class="page-title-box d-sm-flex align-items-center justify-content-between">
          <h4 class="mb-sm-0 text-primary">Gerenciar Meus Pacotes de Shows</h4>
          <span class="badge bg-primary fs-12 p-2">{{ packagesList.length }} de 3 Utilizado</span>
        </div>
      </div>
    </div>

    <div v-if="successMessage" class="alert alert-success border-0 shadow-sm">{{ successMessage }}</div>
    <div v-if="errorMessage" class="alert alert-danger border-0 shadow-sm">{{ errorMessage }}</div>

    <div v-if="loading" class="text-center py-5"><div class="spinner-border text-primary"></div></div>

    <div class="row" v-else>
      <div class="col-xl-7">
        <div class="row" v-if="packagesList.length > 0">
          <div class="col-md-12 mb-3" v-for="item in packagesList" :key="item.id">
            <div class="card border-start border-primary border-3 shadow-sm">
              <div class="card-body p-4">
                <div class="d-flex align-items-start mb-3">
                  <div class="flex-grow-1 pe-3">
                    <h5 class="card-title text-primary mb-1 fw-bold">{{ item.title }}</h5>
                    <p class="text-muted mb-0 fs-12"><i class="ri-time-line"></i> {{ item.durationMinutes }} min</p>
                  </div>
                  <div class="flex-shrink-0 text-end">
                    <h4 class="text-success mb-0 fw-bold">{{ formatCurrency(item.basePrice) }}</h4>
                    <div class="d-flex justify-content-end gap-1 mt-2">
                      <button class="btn btn-sm btn-soft-info" @click="startEditPackage(item)"><i class="ri-pencil-line"></i></button>
                      <button class="btn btn-sm btn-soft-danger" @click="handleDeletePackage(item.id)"><i class="ri-delete-bin-line"></i></button>
                    </div>
                  </div>
                </div>
                <p class="text-muted bg-light p-3 rounded fs-13 border-dashed mb-0">{{ item.description }}</p>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div class="col-xl-5">
        <div class="card">
          <div class="card-body">
            <div class="alert alert-warning text-center" v-if="packagesList.length >= 3 && !isEditing">Catálogo Cheio.</div>
            <form v-else @submit.prevent="handleSubmitForm">
              <div class="mb-3">
                <label class="form-label fw-medium">Título Comercial</label>
                <input type="text" class="form-control" v-model="form.title" required />
              </div>
              <div class="row">
                <div class="col-md-6 mb-3">
                  <label class="form-label fw-medium">Duração (Minutos)</label>
                  <input type="number" class="form-control" v-model.number="form.durationMinutes" required />
                </div>
                <div class="col-md-6 mb-3">
                  <label class="form-label fw-medium">Preço Base do Cachê</label>
                  <input type="text" class="form-control text-end text-success fw-bold" :value="displayPrice" @input="formatInputMoney" required />
                </div>
              </div>
              <div class="mb-4">
                <label class="form-label fw-medium">Descrição</label>
                <textarea class="form-control" rows="4" v-model="form.description" required></textarea>
              </div>
              <button type="submit" :class="isEditing ? 'btn btn-info w-100' : 'btn btn-success w-100'" :disabled="saving">
                {{ saving ? "Processando no MySQL..." : (isEditing ? "Salvar Alterações" : "Cadastrar Formato") }}
              </button>
            </form>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import axios from 'axios';
export default {
  name: "MusicoPacotes",
  data() {
    return {
      loading: true, saving: false, isEditing: false, editingId: null, successMessage: null, errorMessage: null,
      packagesList: [], displayPrice: "R\$ 0,00",
      packagesControl: { currentPackagesCount: 0, canAddMorePackages: true },
      form: { title: "", durationMinutes: 120, basePrice: 0.00, description: "" }
    };
  },
  methods: {
    async loadPackagesData() {
      this.loading = true;
      try {
        const token = localStorage.getItem('jwt');
        const config = { headers: { Authorization: `Bearer ${token}` } };
        const resList = await axios.get(`${process.env.VUE_APP_API_BASE_URL}/tenants/packages`, config);
        this.packagesList = resList.data;
        const responseMe = await axios.get(`${process.env.VUE_APP_API_BASE_URL}/tenants/me`, config);
        this.packagesControl = responseMe.data.packagesControl || { currentPackagesCount: this.packagesList.length, canAddMorePackages: this.packagesList.length < 3 };
        this.loading = false;
      } catch (error) { this.loading = false; this.errorMessage = "Erro de conexão."; }
    },
    formatInputMoney(event) {
      let value = event.target.value.replace(/\D/g, "");
      let floatValue = parseFloat(value) / 100 || 0;
      this.form.basePrice = floatValue;
      this.displayPrice = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(floatValue);
    },
    async handleSubmitForm() {
      this.saving = true;
      try {
        const token = localStorage.getItem('jwt');
        const config = { headers: { Authorization: `Bearer ${token}` } };
        if (this.isEditing) {
          await axios.put(`${process.env.VUE_APP_API_BASE_URL}/tenants/packages/${this.editingId}`, this.form, config);
        } else {
          await axios.post(`${process.env.VUE_APP_API_BASE_URL}/tenants/packages`, this.form, config);
        }
        this.cancelEditMode(); await this.loadPackagesData();
      } catch (error) { this.saving = false; this.errorMessage = "Erro na gravação."; }
    },
    startEditPackage(item) {
      this.isEditing = true; this.editingId = item.id; this.form.title = item.title;
      this.form.durationMinutes = item.durationMinutes; this.form.basePrice = item.basePrice;
      this.form.description = item.description; this.displayPrice = this.formatCurrency(item.basePrice);
    },
    cancelEditMode() { this.isEditing = false; this.editingId = null; this.saving = false; this.form.title = ""; this.form.basePrice = 0.00; this.displayPrice = "R\$ 0,00"; this.form.description = ""; },
    async handleDeletePackage(id) {
      if (!confirm("Excluir?")) return;
      try {
        const token = localStorage.getItem('jwt');
        const config = { headers: { Authorization: `Bearer ${token}` } };
        await axios.delete(`${process.env.VUE_APP_API_BASE_URL}/tenants/packages/${id}`, config);
        await this.loadPackagesData();
      } catch (error) { this.errorMessage = "Erro ao deletar."; }
    },
    formatCurrency(v) { return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(v); }
  },
  mounted() { this.loadPackagesData(); }
};
</script>
```

---

// ArtistAvailability.cs (Tabela de Grade Semanal Fixa de Domingo a Sábado)
[Table("artistavailabilities")]
public class ArtistAvailability
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required]
    public Guid UserId { get; set; }
    [Required]
    public int DayOfWeek { get; set; } // 0 = Domingo, 1 = Segunda ... 6 = Sábado
    [Required]
    public TimeSpan StartTime { get; set; }
    [Required]
    public TimeSpan EndTime { get; set; }
    [Required]
    public bool IsAvailable { get; set; } // Default Off (Falso) para controle reativo
}

// ArtistAgendaBlock.cs (Tabela de Recessos de Emergência / Intervalos de Férias)
[Table("artistagendablocks")]
public class ArtistAgendaBlock
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required]
    public Guid UserId { get; set; }
    [Required]
    public DateTime StartDate { get; set; } // Início do Afastamento
    [Required]
    public DateTime EndDate { get; set; }   // Término do Afastamento
    [Required]
    [StringLength(255)]
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// DTOs e Contratos Oficiais de Entrada/Saída de Dados
public record SaveAvailabilityRequest(int DayOfWeek, string StartTime, string EndTime, bool IsAvailable);
public record CreateBlockRequest(DateTime StartDate, DateTime EndDate, string? Reason);
public record ActionWithNotesRequest(string Notes);

public record ShowRequestItemResponse(
    Guid Id, string ContractorName, string EventName, string EventType, DateTime EventDate,
    string PackageTitle, decimal BasePackagePrice, int DistanceKm, int ExtraKm, decimal ExtraKmValueCharged,
    int RequestedDurationHours, int ExtraHours, decimal ExtraHoursValueCharged, decimal TotalProposedPrice,
    string Status, string Notes
);

// 4.1. PUT: Atualizar Intervalo de Recesso Existente ( api/tenants/agenda/blocks/{id} )
[HttpPut("blocks/{id:guid}")]
public async Task<IActionResult> UpdateBlock(Guid id, [FromBody] CreateBlockRequest model)
{
    var tenantIdLogado = ObterTenantIdLogado();
    var blocoExistente = await _context.ArtistAgendaBlocks
        .FirstOrDefaultAsync(b => b.Id == id && b.UserId == tenantIdLogado);

    if (blocoExistente == null) return NotFound("Recesso não localizado.");
    if (model.StartDate.Date < DateTime.UtcNow.Date) return BadRequest("Data inicial inferior a hoje.");
    if (model.EndDate.Date < model.StartDate.Date) return BadRequest("Data final anterior ao início.");

    blocoExistente.StartDate = model.StartDate.Date;
    blocoExistente.EndDate = model.EndDate.Date;
    blocoExistente.Reason = !string.IsNullOrEmpty(model.Reason) ? model.Reason : "Modificado";

    await _context.SaveChangesAsync();
    return Ok(new { message = "Intervalo de recesso atualizado com sucesso!" });
}

// 5.2. GET: Mapa de Calor do Calendário Mensal Unificado
[HttpGet("calendar-view")]
public async Task<IActionResult> GetCalendarView([FromQuery] int month, [FromQuery] int year)
{
    var tenantIdLogado = ObterTenantIdLogado();
    var disponibilidadesPadrão = await _context.ArtistAvailabilities.AsNoTracking().Where(a => a.UserId == tenantIdLogado).ToListAsync();
    
    // Filtro elástico: Captura intervalos de recessos que cruzam por dentro do mês solicitado
    var bloqueiosDoMes = await _context.ArtistAgendaBlocks.AsNoTracking()
        .Where(b => b.UserId == tenantIdLogado && 
              ((b.StartDate.Month == month && b.StartDate.Year == year) || (b.EndDate.Month == month && b.EndDate.Year == year)))
        .ToListAsync();

    var showsDoMes = await _context.ArtistEvents.AsNoTracking().Include(e => e.ArtistPackage)
        .Where(e => e.UserId == tenantIdLogado && e.EventDate.Month == month && e.EventDate.Year == year && e.Status != "Rejected")
        .ToListAsync();

    int diasNoMes = DateTime.DaysInMonth(year, month);
    var listaEventosCalendario = new List<object>();

    for (int dia = 1; dia <= diasNoMes; dia++)
    {
        var dataAtual = new DateTime(year, month, dia);
        int diaDaSemanaInt = (int)dataAtual.DayOfWeek;
        var showsDoDia = showsDoMes.Where(e => e.EventDate.Date == dataAtual.Date).ToList();
        
        // Avaliação matemática: Verifica se o dia atual do laço cai dentro do range de início/fim do recesso
        var bloqueioExistente = bloqueiosDoMes.FirstOrDefault(b => dataAtual.Date >= b.StartDate.Date && dataAtual.Date <= b.EndDate.Date);

        string corMestre = "light";
        if (showsDoDia.Any(s => s.Status == "Confirmed")) corMestre = "info";
        else if (showsDoDia.Any(s => s.Status == "Pre_Approved")) corMestre = "warning";
        else if (showsDoDia.Any(s => s.Status == "In_Negotiation")) corMestre = "secondary"; // Roxo
        else if (bloqueioExistente != null) corMestre = "danger"; // Vermelho (Trava por Range)
        else {
            var regra = disponibilidadesPadrão.FirstOrDefault(a => a.DayOfWeek == diaDaSemanaInt);
            if (regra != null && regra.IsAvailable) corMestre = "success";
        }

        // ... montagem de subEventosJson simplificada herda propriedades normais ...
        listaEventosCalendario.Add(new { date = dataAtual.ToString("yyyy-MM-dd"), dayOfWeek = diaDaSemanaInt, color = corMestre, events = new List<object>() });
    }
    return Ok(listaEventosCalendario);
}

// Métodos Chave Injetados para Controle de Disponibilidade e Filtro Cronológico
computed: {
  // Varre a memória da RAM local e oculta recessos passados para manter a interface curta e limpa
  listaRecusosFuturos() {
    const hoje = new Date();
    hoje.setHours(0, 0, 0, 0);
    return this.activeBlocks.filter(b => {
      if (!b.startDate && !b.endDate) return false;
      const dataTermino = b.endDate ? new Date(b.endDate) : new Date(b.startDate);
      return dataTermino >= hoje;
    });
  }
},
methods: {
  // Casamento Dinâmico de Dados: Monta a grade estática de 7 posições e liga apenas os ativos no BD
  async loadWeeklyAvailability() {
    const gradePadraoSeteDias = [
      { dayOfWeek: 0, startTime: "19:00:00", endTime: "23:59:00", isAvailable: false },
      { dayOfWeek: 1, startTime: "19:00:00", endTime: "23:59:00", isAvailable: false },
      { dayOfWeek: 2, startTime: "19:00:00", endTime: "23:59:00", isAvailable: false },
      { dayOfWeek: 3, startTime: "19:00:00", endTime: "23:59:00", isAvailable: false },
      { dayOfWeek: 4, startTime: "19:00:00", endTime: "23:59:00", isAvailable: false },
      { dayOfWeek: 5, startTime: "21:00:00", endTime: "03:00:00", isAvailable: false },
      { dayOfWeek: 6, startTime: "18:00:00", endTime: "04:00:00", isAvailable: false }
    ];
    const response = await axios.get(`${process.env.VUE_APP_API_BASE_URL}/tenants/agenda/availability`);
    const dadosDoBanco = response.data || [];
    this.weeklyAvailability = gradePadraoSeteDias.map(diaFixo => {
      const correspondenteNoBanco = dadosDoBanco.find(d => d.dayOfWeek === diaFixo.dayOfWeek);
      return correspondenteNoBanco ? { ...correspondenteNoBanco } : diaFixo;
    });
  },
  // Inversão Operacional: Se editingBlockId tiver ID preenchido executa PUT, senão executa POST
  async handleCreateManualBlock() {
    const payload = { startDate: this.blockForm.startDate, endDate: this.blockForm.endDate, reason: this.blockForm.reason };
    if (this.editingBlockId) {
      await axios.put(`${process.env.VUE_APP_API_BASE_URL}/tenants/agenda/blocks/${this.editingBlockId}`, payload);
    } else {
      await axios.post(`${process.env.VUE_APP_API_BASE_URL}/tenants/agenda/blocks`, payload);
    }
    this.handleCancelEditBlock();
    await this.refreshDashboard();
  },
  handleSetupEditBlock(blockItem) {
    this.editingBlockId = blockItem.id;
    this.blockForm.startDate = blockItem.startDate.split('T')[0];
    this.blockForm.endDate = blockItem.endDate.split('T')[0];
    this.blockForm.reason = blockItem.reason;
  },
  async handleDeleteBlock(blockId) {
    if (confirm("Cancelar este recesso?")) {
      await axios.delete(`${process.env.VUE_APP_API_BASE_URL}/tenants/agenda/blocks/${blockId}`);
      await this.refreshDashboard();
    }
  }
}

---

## 💳 6. GESTÃO DE MENSALIDADES, WEBHOOKS E UPGRADES (MOMENTO 2)

### 6.1 Topologia de Contratos do Webhook Financeiro (`SaaSWebhooksController.cs`)
*   **Ajuste Crítico de Entrada:** O gateway Asaas transmite atualizações de faturamento cronológico (criação, liquidação e atraso) dentro de um nó chamado `payment`, enviando apenas o ID de referência do contrato dentro de `subscription`. Os records foram expandidos para evitar exceções nulas:
```csharp
public record AsaasWebhookPayload(string @event, WebhookSubscriptionData? subscription, WebhookPaymentData? payment);
public record WebhookSubscriptionData(string id, string customer, string status);
public record WebhookPaymentData(string id, string subscriptionId, decimal value, string status, string paymentMethod, string? pixCopyPaste, DateTime dueDate);
```
*   **Roteamento de Eventos (Switch-Case):**
    1.  `PAYMENT_CREATED`: Intercepta mensalidades geradas na nuvem. Executa uma query no MariaDB para localizar o `UserId` vinculado à assinatura e insere uma nova linha na tabela `saas_invoices` com status inicial `"PENDING"` e o código Pix Copia e Cola (`pixCopyPaste`).
    2.  `PAYMENT_RECEIVED`: Modifica o status da assinatura mestre para `"Active"`, altera o status do músico na tabela `Users` para `"Active"` e executa um update cirúrgico na linha específica da fatura na tabela `saas_invoices` para `"PAYMENT_RECEIVED"`.
    3.  `PAYMENT_OVERDUE`: Transforma a assinatura mestre em `"PastDue"`, altera o perfil do músico para `SubscriptionStatus = "PastDue"` / `ProfileStatus = "Pending_Payment"` (trancando o painel via Navigation Guard) e altera o status da fatura específica na tabela `saas_invoices` para `"PAYMENT_OVERDUE"`.

### 6.2 Restauração e Assinatura do Endpoint no Backend (`TenantsController.cs`)
*   **Correção de Rota Fantasma:** O método de migração foi reinjetado de raiz com o verbo HTTP `POST` e a URL exata consumida pelo Axios:
```csharp
// POST: api/tenants/me/migrate-plan
[HttpPost("me/migrate-plan")]
[Authorize(Roles = "Tenant")]
public async Task<IActionResult> MigratePlan([FromBody] MigratePlanRequest request);
public record MigratePlanRequest(string NewSaaSPlanId);
```
*   **Mapeamento de Tipos em RAM (`MigrarPlanos.vue`):** Para sanar colisões entre GUIDs textuais/UUIDs e strings numéricas originadas no JSON, a checagem por igualdade estrita (`===`) no front-end foi desativada. Injeção do método `isCurrentPlan(plan)` que normaliza os IDs em `.toString().trim().toLowerCase()`. Caso as chaves divirjam, executa um fallback de segurança comparando os nomes textuais dos planos (Ex: `"bronze"` com `"bronze"`).
*   **Nomenclatura JSON de Rede:** A serialização do DTO do .NET 10 entrega o campo do plano logado em camelCase como `responseMe.data.saasPlanId`. O front-end foi parametrizado com fallbacks de leitura automática para este formato.

---

## ⚠️ 7. AUDITORIA TRIDIMENSIONAL DE DOWNGRADE (ABORDAGEM A)

### 7.1 Regra de Interceptação Preventiva no Front-End
*   **Logística de Bloqueio:** Para impedir que músicos migrem de planos maiores para menores contendo volumetria de mídias/pacotes superior ao teto permitido no plano de destino, o front-end intercepta o clique do botão antes do disparo do Axios e roda a seguinte matriz matemática baseada no estado reativo `currentInventory`:
    1.  *Auditoria de Galeria:* `currentInventory.photosCount > targetPlan.maxPhotosCount`
    2.  *Auditoria de EPK/Vídeos:* `currentInventory.videosCount > targetPlan.maxVideosCount`
    3.  *Auditoria Comercial:* `currentInventory.packagesCount > targetPlan.maxShowsPerMonth`
*   **UX Educativo do Velzon:** Se qualquer item falhar, a requisição é abortada e ativa-se o estado `downgradeModal.show = true`. Um modal interativo abre em tela exibindo o checklist de excessos detalhado e a ação necessária (Ex: *"Fotos no Portfólio: Você possui 8 itens, mas o limite é 5. Ação: É necessário excluir 3 itens"*).

### 7.2 Correção Semântica e Sincronização Elástica de Cotas (De 3 para 5)
*   **Estratégia de Alias de Banco:** Para blindar o sistema contra retrabalhos e quebras de migrations, a coluna física **`MaxShowsPerMonth`** da tabela `saasplans` no MariaDB foi mantida com o nome original no C#, mas passou a atuar como o **Limite Máximo de Formatos de Shows cadastrados na vitrine** do artista.
*   **Dinamização das Métricas de Consumo (`TenantsController.cs`):** O número rígido `3` chumbado no retorno JSON foi expurgado. O endpoint lê a propriedade virtual de herança do EF Core `.Include(u => u.CurrentPlan)` e distribui os tetos reais dinamicamente:
```csharp
// GET: api/tenants/dashboard-metrics
totalPacotesPermitidos = user.CurrentPlan?.MaxShowsPerMonth ?? 3,
pacotesRestantesDisponiveis = Math.Max((user.CurrentPlan?.MaxShowsPerMonth ?? 3) - totalPacotes, 0)
```
*   **Reatividade nas Views (`Assinatura.vue` e `Pacotes.vue`):** Ambas as telas foram limpas de travas numéricas estáticas. Elas consomem o campo dinâmico `totalPackagesPermitidos` da API. No cenário onde o limite foi expandido para **5** no banco, o chassi visual do Vue 3 reajusta dinamicamente a régua de progresso (Ex: Redimensionamento automático de `3 de 3` para `3 de 5`, reduzindo a barra azul para 3% de consumo de cota e liberando as travas dos inputs e do botão "Cadastrar Formato" instantaneamente).

---

## 💼 8. MÓDULO DE CARTEIRA DIGITAL E COMPLIANCE BANCÁRIO (`Carteira.vue`)

### 8.1 Arquitetura Estável do Fluxo de Onboarding e Estados Elásticos
*   **Controle de Fluxo por Estados (Três Cenários):** A interface gráfica do monitor transmuta-se de forma automática em tempo real baseada na leitura do metadado `accountStatus` extraído do banco pelo front-end:
    1.  *Cenário A (`NOT_CREATED`):* Exibe o banner mestre de ativação do Velzon instruindo o artista a inicializar a conta. O clique executa o `POST /connect` gerando a subconta bancária.
    2.  *Cenário B (`PENDING`):* Trava as operações comerciais. Exibe o link seguro externo para envio de mídias de KYC no Asaas e aciona o botão de engenharia local em Sandbox `handleSimulateApproval`. O front-end injeta um aviso de compliance bloqueando preventivamente o botão pública "Contratar Show" para proteger o ecossistema.
    3.  *Cenário C (`APPROVED`):* Esconde permanentemente os blocos de onboarding. Libera os painéis em gradiente verde e cinza escuro de faturamento e ativa o grid do histórico de extratos.

### 8.2 Filtragem Multi-Critério de Extratos Financeiros em RAM Local
*   **Funil Matemático Computado (`transacoesFiltradas`):** Para evitar requisições pesadas e repetitivas de escrita contra o MySQL, a interface utiliza seletores de busca instantânea rodando diretamente na memória RAM local do navegador através de uma propriedade computada tripla:
    *   *Filtro de Operação:* Segrega reativamente os registros por `Receivable` (Entrada de cachê custodiado) ou `Payout` (Saques e transferências reais realizadas).
    *   *Filtro de Status:* Isola lançamentos pelo status booleano `isReleased`, mapeando visualmente em badges dinâmicos se o cachê está `"Liberado"` para saque ou `"Retido"` sob custódia temporária pós-evento.
    *   *Filtro Cronológico Fixo:* Aplica cortes em tempo de execução na linha do tempo limitando a visualização pelo mês corrente (Setembro/2026) ou retrocedendo uma janela exata de 30 dias com o reset matemático de horas (`00:00:00`).

## 🌐 9. CAMADA PÚBLICA: CATÁLOGO DE ATRAÇÕES E EPK MULTIMÍDIA (SESSÃO DO CONTRATANTE)

### 9.1 Catálogo Geral de Artistas com Filtros Rápidos (`CastingView.vue`)
*   **Consumo Dinâmico Conectado:** Migração completa da listagem estática simulada para consumo assíncrono real via Axios, batendo direto no endpoint anônimo do .NET 10 (`GET /public/artists`).
*   **Tratamento de Serialização (PascalCase vs camelCase):** Para blindar o front-end contra estouros de tipo e chaves `undefined` geradas pela desserialização do `System.Text.Json`, o loop `v-for` e as propriedades computadas foram parametrizados com checagem dupla reativa (`artista.nomeBanda || artista.NomeBanda`), garantindo estabilidade imediata dos dados textuais na DOM.
*   **Filtro Preventivo de Regra de Negócio:** Injeção de uma trava cirúrgica na computada `artistasFiltrados()` para ignorar e ocultar de forma permanente qualquer registro cujo nome contenha a palavra *"ADMINISTRADOR"*. O usuário master serve apenas para gerência de auditoria e testes de Sandbox, ficando proibido de ser exposto para contratação comercial.
*   **Resolução de Portas de Mídia e Arquivos Estáticos:** Como os links relativos de imagens de capa vêm gravados do MariaDB (Ex: `/uploads/artist-medias/...`), o método utilitário `obterUrlImagem` foi implementado para limpar dinamicamente o sufixo `/api` da variável de ambiente `${process.env.VUE_APP_API_BASE_URL}` se necessário, forçando o navegador a buscar os binários físicos direto na porta do Kestrel (`:5297/uploads/...`), sanando falhas de erro 404.

### 9.2 Perfil Premium do Artista e Contratação Direta (`ArtistaPerfilPublico.vue`)
*   **Roteamento Dinâmico de Vendas:** Registro da rota pública parametrizada por URL amigável (`path: "/artista/:slug"`) para servir de Kit de Imprensa Eletrônico (EPK) do músico focado em e-commerce de contratação direta, sem barreiras de pedidos de orçamentos.
*   **Ajuste Cinemático do Hero Topo:** O visual do banner superior foi reformulado seguindo a estética de plataformas de streaming (padrão Spotify). Removeu-se o chassi de fundo desfocado (*blur*) e a caixa menor flutuante de logotipo sobreposto. A foto de capa original passa a ocupar 100% da largura do contêiner widescreen (`height: 420px`), calibrada com opacidade total (`opacity: 1;`) e protegida por um gradiente escuro linear inferior em CSS para ressaltar a leitura do Nome da Banda em fonte display branca.
*   **E-commerce Responsivo de Pacotes:** O cabeçalho dos cards comerciais de pacotes da tabela `artist_packages` foi reestruturado para evitar colisões visuais. Substituiu-se o alinhamento flex lateral por um empilhamento em bloco vertical com largura total (`w-100` / `text-wrap`). Os títulos dos formatos de shows expandem-se por completo sem cortes por reticências e os badges explicativos de tempo de apresentação assentam-se de forma fixa na linha inferior (`Duração: X min de show`).

### 9.3 Engenharia Avançada de Portfólio Multimídia e Visualizador Clicável
*   **Parser Avançado de Mídias e Vídeos:** Implementação do método utilitário `converterLinkYoutube` operando com tratamento rigoroso de substrings e fatiamento fixo por índices (`indexOf`, `substring`, `split`). A rotina isola com precisão cirúrgica os 11 caracteres do ID único do vídeo gravado na tabela `artist_medias` pelo painel (Ex: `bxo9mtJjvs0`), seja ele originado de links curtos (`youtu.be/`), links de busca (`watch?v=`) ou códigos de incorporação de terceiros (`embed/`).
*   **Player Mestre com Esteira Reativa de Thumbnails (UX Premium):** Para mitigar a fadiga de rolagem vertical gerada pelo empilhamento de múltiplos players grandes, o layout da galeria foi condensado:
    1.  *Cinema Principal:* Um único elemento `<iframe>` responsivo fixado no topo renderiza o vídeo correspondente ao estado dinâmico `videosPortfolio[videoAtivoIndex].youtubeId`.
    2.  *Esteira de Miniaturas:* Se houver mais de um clipe gravado no MariaDB, o Vue projeta uma fileira horizontal de botões clicáveis, puxando automaticamente as imagens de miniatura oficiais direto dos servidores do Google (`://youtube.com`). O clique em qualquer miniatura atualiza o índice e comuta o vídeo em execução em tempo de execução de RAM.
*   **Visualizador Incorporado Inline de Fotografia:** O grid inferior de imagens secundárias foi dotado de interatividade reativa ligada ao estado `fotoAtivaGrande`. Ao passar o mouse, o cursor assume o visual de uma lupa (`cursor: zoom-in`) e, ao clicar, a lógica oculta temporariamente o player de vídeo e abre a imagem ampliada em alta resolução dentro do mesmo chassi de dimensões widescreen do topo (`ratio ratio-16x9`), acompanhada de um botão de escape de fechamento para restaurar o estado das mídias.

### 9.4 Endpoint de Detalhes Estendidos no Backend (`PublicArtistsController.cs`)
```csharp
// GET: api/public/artists/{slug}
// Endpoint público, anônimo e otimizado para projeção LINQ assíncrona contra o MariaDB
[HttpGet("{slug}")]
public async Task<ActionResult> GetArtistBySlug([FromRoute] string slug)
{
    if (string.IsNullOrWhiteSpace(slug)) return BadRequest(new { mensagem = "O slug é obrigatório." });

    var artista = await _context.Users
        .AsNoTracking()
        .Where(u => u.Slug == slug.Trim().ToLower() && u.ProfileStatus == "Active")
        .Select(u => new {
            Id = u.Id,
            NomeBanda = u.Name,
            Slug = u.Slug,
            EstiloMusical = u.EstiloMusical ?? "Geral",
            FormatoArtístico = u.FormatoArtístico ?? "Banda",
            Slogan = u.Slogan ?? string.Empty,
            Biografia = u.Biografia ?? string.Empty,
            
            CidadeAtendida = _context.ArtistAddresses.Where(a => a.UserId == u.Id).Select(a => a.City).FirstOrDefault() ?? "Não Informada",
            State = _context.ArtistAddresses.Where(a => a.UserId == u.Id).Select(a => a.State).FirstOrDefault() ?? string.Empty,
            FotoCapaUrl = _context.ArtistMedias.Where(m => m.UserId == u.Id && (m.MediaType == "Cover" || m.MediaType == "cover")).Select(m => m.MediaUrl).FirstOrDefault() ?? string.Empty,

            Pacotes = _context.ArtistPackages.Where(p => p.UserId == u.Id).Select(p => new {
                Id = p.Id, Title = p.Title, Description = p.Description, DurationMinutes = p.DurationMinutes, BasePrice = p.BasePrice
            }).ToList(),

            Medias = _context.ArtistMedias.Where(m => m.UserId == u.Id).Select(m => new {
                Id = m.Id, MediaUrl = m.MediaUrl, MediaType = m.MediaType
            }).ToList()
        }).FirstOrDefaultAsync();

    if (artista == null) return NotFound(new { mensagem = "Artista não localizado." });
    return Ok(artista);
}
```


## 🏁 FIM DO STATE SNAPSHOT: INFRAESTRUTURA CONSOLIDADA
