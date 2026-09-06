using System;
using System.Collections.Generic;

namespace SevenShows.Api.Modules.Tenants.Models;

public class DashboardMetricsDto
{
    public int ShowsEsteMes { get; set; }
    public decimal CachesAReceber { get; set; }
    public int PacotesCriados { get; set; }
    public int TotalPacotesPermitidos { get; set; } = 3;
    
    // NOVAS PROPRIEDADES ADICIONADAS PARA CONTROLAR OS SALDOS EM TEMPO REAL
    public int PacotesRestantesDisponiveis { get; set; }
    public int FotosRestantesDisponiveis { get; set; }
    public int TotalFotosPermitidasNoPlano { get; set; }

    public int RaioDeslocamentoKm { get; set; }
    public int ProgressoEpkPercentual { get; set; }
    public List<ProximoShowDto> ProximosShows { get; set; } = new List<ProximoShowDto>();
}

public class ProximoShowDto
{
    public string Data { get; set; } = string.Empty;
    public string MesAno { get; set; } = string.Empty;
    public string NomeEvento { get; set; } = string.Empty;
    public string Local { get; set; } = string.Empty;
    public string CidadeEstado { get; set; } = string.Empty;
}
