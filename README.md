<div align="center">

<img src="./images/tim.png" alt="Bit Icon" width="35%" />

### tim - timeføring cli for Blank
[![License: MIT](https://img.shields.io/badge/License-MIT-05bd7e.svg)](LICENSE)
[![Terminal](https://img.shields.io/badge/interface-terminal-05bd7e.svg)](https://github.com/blankoslo/tim)

[Features](#-features) • [Installation](#-installasjon) • [Usage](#-bruk) 

<img src="./images/icon.512x512.png" alt="Bit Icon" width="50px" />
</div>



## ✨ Features

| **Feature**                             | **Description**                                                                                |
| --------------------------------------- | ---------------------------------------------------------------------------------------------- |
| **⏲️ Timeføring**               | Før timer for 1 dag, hele uka, eller hele måneden|
| **🗓️ Rapporter**              | Gjennomgå timeføring for hele måneden, vise alle timer for alle et prosjekt               |
| **🤓 Ingen browser**         | Du slipper browserens tamme klør                                      |


## 🚀 Installasjon

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

Flere muligheter. Dersom man bruker WSL, så kan man bruke Homebrew. Se over.

Ellers har man 3 muligheter

1 - .NET tool

 (krever .NET 9 eller .NET 10):

Et _GitHub Classic Token_  kan du opprette [her](https://github.com/settings/tokens). Husk å gi den `read:packages` og litt varighet da

```bash
$ dotnet nuget add source "https://nuget.pkg.github.com/blankoslo/index.json" \
 --username dontcare \ 
 --password <GH_PAT> \ 
 --store-password-in-clear-text 
 --name github \ 
```

```bash
$ dotnet tool install --global BlankDev.Tools.Tim --source "github" 
```

Evt, dersom du vil håndtere nye versjoner selv:
```bash
$ dotnet tool install --global BlankDev.Tools.Tim \
 --source "/folder/med/nedlasted/BlankDev.Tools.Tim.0.1.0.nupkg" \ 
```
2 - Manuelt

Last ned `tim.exe` fra [releases](https://github.com/blankoslo/tim/releases/latest) og legge til i PATH.


3 - `dnx`
(Krever .NET 10+)

Uten installasjon. 

```bash
dnx BlankDev.Tools.Tim
```
NB, krever en `nuget.config` m/ source "https://nuget.pkg.github.com/blankoslo/index.json" og et API key med read:packages rettighet.

</details>



## 💻 Bruk

```bash
$ tim write --help
Usage: write [arguments...] [options...] [-h|--help] [--version]

Registrerer nye timer

Arguments:
  [0] <decimal?>    Antall timer som skal føres

Options:
  -p, --project <string?>        Prosjektkoden til prosjektet. Bruker global default-prosjekt hvis ikke angitt
  -r, --range <SelectedRange>    Hvilken uke som skal timeføres. Gyldige: "Current|Previous"
  -d, --date <string?>           Dato som skal føres, dd.MM Default dagens dato.
  -y, --yes <bool?>              Bare kjørr, ikke spør om bekreftelser.
```




```bash
# 7.5 timer på prosjekt ANE1006 for i dag
tim write -p ANE1006 
```

```bash
# 7.5 timer på prosjekt ANE1006 for idag
$ tim set-default ANE1006
$ tim write 
```

```bash
# 3.5 timer istedet for defaulten 7,5
$ tim write -h 3,5
```
