param([ValidateRange(44, 512)][int]$Size = 132)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$sourcePath = Join-Path $projectRoot 'frontend/InvestmentTracker.Web/design/fd-logo-original.png'
$outputPath = Join-Path $projectRoot 'frontend/InvestmentTracker.Web/public/fd-logo.png'

Add-Type -AssemblyName System.Drawing
$source = [System.Drawing.Image]::FromFile($sourcePath)
try {
    $resized = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($resized)
        try {
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($source, [System.Drawing.Rectangle]::new(0, 0, $Size, $Size),
                0, 0, $source.Width, $source.Height, [System.Drawing.GraphicsUnit]::Pixel)
            $resized.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
        } finally { $graphics.Dispose() }
    } finally { $resized.Dispose() }
} finally { $source.Dispose() }

Get-Item -LiteralPath $outputPath | Select-Object FullName, Length
