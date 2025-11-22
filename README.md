# tim

timeføring cli

```terminal
$ tim --help
Usage: [command] [-h|--help] [--version]

Commands:
  get-default    Henter default-prosjektet ditt
  list, ls       Lister førte timer
  login          Logger inn via browser
  logout         Logger deg ut lokalt
  set-default    Setter et prosjekt som default til timeføring
  write          Registrerer nye timer
```


## install

### MacOS/Linux

HomeBrew

```bash
brew tap blankoslo/tools git@github.com:blankoslo/homebrew-tools.git
brew install blankoslo/tools/tim
```

###
Eller gå til [releases](https://github.com/blankoslo/tim/releases/latest) og last ned siste versjon.

<details>

<summary>Windows</summary>

Flere muligheter. Dersom man bruker WSL, så kan man bruke Homebrew. Ellers:

1) .NET tool fra en nuget feed:
```bash
$ dotnet tool install --global tim \
 --source "https://nuget.pkg.github.com/blankoslo/index.json"
```

```bash
$ dotnet tool install --global tim \
 --source "/folder/med/nedlasted/tim.0.1.0.nupkg"
```

2) Last ned exe fra [releases](https://github.com/blankoslo/tim/releases/latest) og legge til i PATH.


</details>


### usage
