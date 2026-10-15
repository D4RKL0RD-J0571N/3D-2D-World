$sourceDir = "D:\Development\Game Development\3D 2D World\Assets\Sprites"
$files = Get-ChildItem -Path $sourceDir -Recurse -Filter *.png

foreach ($file in $files) {
    $name = $file.BaseName
    if ($name -notmatch '_NORMAL_MAP' -and $name -notmatch '_HEIGHT_MAP') {
        $sourcePath = $file.FullName
        $parentDir = $file.DirectoryName

        # Handle Normal Map
        $normalPath = Join-Path $parentDir ($name + '_NORMAL_MAP.png')
        if (Test-Path $normalPath) { Remove-Item $normalPath -Force }
        Copy-Item $sourcePath $normalPath
        Write-Host "Created Normal Map: $normalPath"

        # Handle Height Map
        $heightPath = Join-Path $parentDir ($name + '_HEIGHT_MAP.png')
        if (Test-Path $heightPath) { Remove-Item $heightPath -Force }
        Copy-Item $sourcePath $heightPath
        Write-Host "Created Height Map: $heightPath"
    }
}
