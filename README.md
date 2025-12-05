<div align="center">

<img src="./images/tim.png" alt="Bit Icon" width="35%" />

### tim - timeføring cli for Blank
[![License: MIT](https://img.shields.io/badge/License-MIT-05bd7e.svg)](LICENSE)
[![Terminal](https://img.shields.io/badge/interface-terminal-05bd7e.svg)](https://github.com/blankoslo/tim)
[![kjøregår](https://img.shields.io/badge/kjøre-går-FFFCB6.svg)](https://www.blank.no/tim-the-incredible-machine)
[![Terminal](https://img.shields.io/badge/tim-the_incredible_machine_-DA2FBF.svg)](https://www.google.com/search?q=tim+the+incredible+machine)



[features](#-features) • [installasjon](#-installasjon) • [ bruk](#-bruk) 

<img src="./images/tim-reel.gif" alt="Bit Icon" width="100%" />
</div>



## ✨ features

| **Feature**                             | **Description**                                                                                |
| --------------------------------------- | ---------------------------------------------------------------------------------------------- |
| **⏲️ Timeføring**               | Før timer for 1 dag, hele uka, eller hele måneden                                      |
| **🗓️ Rapporter**              | Gjennomgå timeføring for deg selv, eller alle hos din kunde/prosjekt               |
| **🤓 Ingen browser**         | Du slipper browserens tamme klør                                      |
| **🌊 Pipe-støtte**               | Kombiner med andre CLI-verktøy for avanserte arbeidsflyter                                      |
| **🕊️ cURL**               | Støtte for å cURL'e fritt mot PostgREST-APIet dersom du ønsker å gå ned på metallet|


## 🚀 installasjon

```bash
# Homebrew
brew tap blankoslo/tools git@github.com:blankoslo/homebrew-tools.git
brew install blankoslo/tools/tim
```

Evt last ned filene fra [releases](https://github.com/blankoslo/tim/releases/latest). 
Last ned siste versjon og hiv den i en mappe som er i PATH-en din.


*Windows*
<details>

<summary>Windows-greier her</summary>


### Alternativ 1:* Som et .NET tool (.NET 9 el 10).

NuGet'en finnes i GitHub Packages feed'en til Blank Oslo (krever .NET 9 eller .NET 10). Autentisering mot feeed krever et _GitHub Classic Token_ (`<GH_PAT>`)  som du kan opprette [her](https://github.com/settings/tokens). Husk å gi den `read:packages` og litt varighet.

```bash
dotnet nuget add source "https://nuget.pkg.github.com/blankoslo/index.json" \
 --username dontcare \ 
 --password <GH_PAT> \ 
 --store-password-in-clear-text \
 --name github \ 

dotnet tool install --global BlankDev.Tools.Tim --source "github" 

# Nedlasted nuget fra Releases:
dotnet tool install --global BlankDev.Tools.Tim \
 --source "/downloads/BlankDev.Tools.Tim.0.1.0.nupkg" \ 
 
# Evt one-off via `dnx` 
dnx BlankDev.Tools.Tim --add-source github
```
### Alt 2 - last ned windows `.exe`

Last ned `tim.exe` fra [releases](https://github.com/blankoslo/tim/releases/latest). Legg evt til i PATH.


</details>



## 💻 Bruk

```bash
$ tim write --help
Usage: write [arguments...] [options...] [-h|--help] [--version]

Registrerer nye timer

Arguments:
  [0] <decimal?>    Antall timer som skal føres

Options:
  -p, --project <string?>        Prosjektkoden til prosjektet. Bruker default-prosjekt hvis ikke angitt.
  -r, --range <SelectedRange>    Valgfritt. Fører hele uka i en go.
  -d, --date <string?>           Dato som skal føres, dd.MM  Default dagens dato.
  -y, --yes <bool?>              Bare kjørr, ikke spør om bekreftelser eller annet mas.
```




```bash
# 7.5 timer på prosjekt ANE1006 for i dag
tim write -p ANE1006 
```

```bash
# 7.5 timer på prosjekt ANE1006 for idag
tim set-default ANE1006
tim write 
```

```bash
# 3.5 timer istedet for defaulten 7,5
tim write -h 3,5
```


`tim` støtter piping:

```bash
# Hente ut ukesrapport for alle hos kunden 'Aneo Mobility'
tim emp ls -c "Aneo Mobility"  --ids | tim ls
```

<div align="center">
<img src="./images/WeeklyReportSample.png" width="75%" />
</div>

```bash
# Hente ut månedsrapport for alle hos kunden 'Aneo Mobility' for forrige måned
tim emp ls -c "Aneo Mobility"  --ids | tim ls --range PreviousMonth
```

<div align="center">
<img src="./images/MonthlyReportSample.png" width="100%" />
</div>


# tim curl

`tim curl` gjør requests rett mot PostgREST APIet med innloggede credentials. 

```bash
# Hva er det dissa folka driver med egentlig?
tim curl '/employees?select=first_name,last_name&role=eq.Annet&termination_date=is.null' 

# -x POST for å kalle RPC-metoder:
$ tim curl -x post '/rpc/employees_on_projects' \ 
 --data '{ "from_date": "2025-11-01", "to_date":"2025-11-30"}' | grep "Ruter"

# Finne timeføringa til alle Mags 
tim curl '/employees?select=id&first_name=like.*Mag*'  | jq -r '.[].id' | tim ls

╭──────────────────────────────┬───────┬───────┬───────┬───────┬───────╮
│                              │ 01.12 │ 02.12 │ 03.12 │ 04.12 │ 05.12 │
├──────────────────────────────┼───────┼───────┼───────┼───────┼───────┤
│ SAL1000 Salg & markedsføring │  7,5  │  7,5  │  7,5  │  7,5  │  7,5  │
│ Daglig sum                   │  7,5  │  7,5  │  7,5  │  7,5  │  7,5  │
│ Ukesum                       │       │       │       │       │ 37,5  │
╰──────────────────────────────┴───────┴───────┴───────┴───────┴───────╯
                              uke 49 Backer                             
╭───────────────────────────────┬───────┬───────┬───────┬───────┬───────╮
│                               │ 01.12 │ 02.12 │ 03.12 │ 04.12 │ 05.12 │
├───────────────────────────────┼───────┼───────┼───────┼───────┼───────┤
│ ADM1000 Administrasjon        │   -   │   -   │  4,5  │   -   │   -   │
│ SB11005 Teamleder BM Betalin… │  7,5  │  8,5  │  3,0  │  7,5  │  7,5  │
│ Daglig sum                    │  7,5  │  8,5  │  7,5  │  7,5  │  7,5  │
│ Ukesum                        │       │       │       │       │ 38,5  │
╰───────────────────────────────┴───────┴───────┴───────┴───────┴───────╯
                             uke 49 Davidsen       
```

### Floq API tips

Floq har en [Swagger spec](https://api-prod.floq.no/) som du kan utforske. Denne _kan_ lastes opp i https://editor.swagger.io/, men for å unngå browser-lus (🤮) , bruk [`github.com/plutovoq`]((https://github.com/plutov/oq))q


```bash
brew install plutov/tap/oq
```


```bash
# last ned swagger 2.0 json
tim curl '/' > floq-openapi.json
# konverter til openapi 3.0 og åpne i oq
npx swagger2openapi floq-openapi.json | oq
```

<div align="center">
<img src="./images/oq.png" width="100%" />
</div>


NB Man _kan_ også gå gi direkte mot Floq-API'et, MEN obs: da vises kun RPC-metodene.

```bash
npx swagger2openapi https://api-prod.floq.no/ | oq
```
