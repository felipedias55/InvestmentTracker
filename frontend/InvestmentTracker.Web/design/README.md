# Logo original

`fd-logo-original.png` preserva o PNG original de 1254 × 1254 pixels com transparência. Esta pasta não é publicada pelo build Angular.

O cabeçalho e o favicon usam `public/fd-logo.png`, uma versão de 132 × 132 pixels para atender até três vezes os 44 pixels de exibição no desktop (30–40 pixels nas telas menores), preservando desenho, proporção e alpha.

Para gerar novamente a versão publicada no Windows, a partir da raiz do repositório:

```powershell
powershell -NoProfile -File scripts/Optimize-BrandLogo.ps1
```

O script usa somente System.Drawing e reamostragem bicúbica; não gera um desenho novo. Não redimensione a versão pequena para produzir outras resoluções: use sempre o original.
