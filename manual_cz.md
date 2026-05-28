## Uživatelská příručka — WFC-ACO-Hybrid generátor prostředí

## Spuštění aplikace

Aplikace je distribuována jako spustitelný Unity build pro operační systém Windows a Linux. Po spuštění se zobrazí hlavní menu s výběrem konfigurace generátoru.

---

## Hlavní menu

Po startu aplikace se zobrazí panel se třemi nastavitelnými parametry:

**1. Velikost mřížky (Grid size)**

Volba určuje rozměry generovaného prostředí. Dostupné předvolby:

| Označení | Rozměry (X × Y × Z) | Buněk | Typ |
|---|---|---|---|
| Small 2D  | 10 × 1 × 10 | 100   | 2D |
| Medium 2D | 25 × 1 × 25 | 625   | 2D |
| Large 2D  | 50 × 1 × 50 | 2 500 | 2D |
| Small 3D  | 10 × 3 × 10 | 300   | 3D |
| Medium 3D | 20 × 4 × 20 | 1 600 | 3D |
| Large 3D  | 30 × 5 × 30 | 4 500 | 3D |

**2. Algoritmus (Algorithm)**

| Možnost | Popis |
|---|---|
| **Pure WFC** | Čistý Wave Function Collapse bez předpočítané trasy. Rychlý, ale na velkých mapách náchylný ke kontradikci (zejména na 2D). |
| **GLS Crawler** | Horolezecký algoritmus (Hill Climbing) předpočítá trasu, poté WFC zaplní okolí. Vhodný pro malé a střední konfigurace. |
| **ACO Hybrid** | Optimalizace mravenčí kolonie předpočítá páteřní trasu. Doporučeno pro střední a velké konfigurace. |

> **Doporučení:** Pro Large 2D (50 × 1 × 50) a používejte
> výhradně ACO Hybrid. GLS Crawler na těchto konfiguracích pravděpodobně
> selže. Systém na tuto kombinaci upozorní varovnou hláškou.

**3. Generující seed**

Volitelné textové pole pro seed umožňuje zadat celé číslo pro deterministickou reprodukci konkrétního výsledku. Je-li prázdné, je seed vygenerován náhodně a jeho hodnota je zobrazena v rozhraní výsledku.

---

## Generování prostředí

Po výběru parametrů stiskněte tlačítko **GENERATE**.

Systém automaticky:
1. Inicializuje mřížku a spustí zvolený algoritmus.
2. V případě selhání (kontradikce) opakuje generaci — maximální počet pokusů
   je škálován automaticky podle velikosti mřížky, ACO má pevně dané tři pokusy.
3. Po úspěšném vygenerování zobrazí výsledné 3D/2D prostředí.

Průběh je indikován stavovou hláškou v levém horním rohu obrazovky
(*SUCCESS* / *FAILED* / počet pokusů, případně délka postavené cesty).

---

## Pohyb ve vygenerovaném prostředí

| Klávesa / akce | Funkce |
|---|---|
| `W` / `S` | Pohyb vpřed / vzad |
| `A` / `D` | Pohyb vlevo / vpravo |
| `Pohyb myší` | Otočení pohledu |
| `E` | Pohyb nahoru |
| `Q` | Pohyb dolů |
| `Left Shift` | Sprint |
| `ESC` | Návrat do hlavního menu |

Kamera má volný pohyb — nejsou uplatněna žádná kolizní omezení.

---

## Minimapa

V pravém horním rohu obrazovky je zobrazena 2D minimapa aktuálního patra.
Minimapa se automaticky přepíná na patro odpovídající aktuální výšce kamery.
Poloha kamery je znázorněna kolečkem na minimapě.

---

## Diagnostická vizualizace (feromonová heatmapa)

Pokud ACO Hybrid selže ve všech pokusech, aktivuje se automaticky vizualizace
feromonové matice. Uzly mřížky jsou zobrazeny jako prostorové objekty:

- **Modré uzly** (malé) — nízká koncentrace feromonů - oblast nebyla agenty
  aktivně prozkoumávána.
- **Červené uzly** (velké) — vysoká koncentrace - oblast byla intenzivně navštěvována.

Tato vizualizace slouží k diagnostice — identifikuje oblasti, v nichž agenti
opakovaně selhávali při hledání průchozí trasy.

---

## Režim izolované trasy (Path Only Mode)

Pokud GLS Crawler úspěšně sestaví páteřní trasu, ale následný WFC průchod
skončí kontradikčním stavem, systém přejde do režimu izolované trasy. V tomto
režimu jsou zobrazeny pouze buňky tvořící páteřní trasu (zvýrazněny žlutě).
Okolní prostředí není instancováno.

Tento režim je dostupný výhradně pro jednopodlažní (2D) konfigurace.

