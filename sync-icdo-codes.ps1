param(
    [string]$CodeSystemPath = "import/fhir/CodeSystem/icdo.json",
    [string]$ValueSetPath = "import/fhir/ValueSet/icdo-topo-bones.json"
)

# Resolve full paths
$repoRoot = Resolve-Path "."
$csPath = Join-Path $repoRoot $CodeSystemPath
$vsPath = Join-Path $repoRoot $ValueSetPath

Write-Host "Loading CodeSystem: $csPath"
$cs = Get-Content $csPath -Raw | ConvertFrom-Json

Write-Host "Loading ValueSet: $vsPath"
$vs = Get-Content $vsPath -Raw | ConvertFrom-Json

# ============================================================
# DEFINE ALL NEW CODES (code, display, parent)
# ============================================================
# Using the questionnaire display texts (exact), with user-approved fixes:
#   - C40.3.410 -> "1. Metatarsal bone", C40.3.419 -> "Metatarsal bone"
#   - C41.0.109 -> "Calvarium" (override VS "Cranium")
#   - C41.2.231..239 for lumbar vertebrae

$newCodes = @(
    # C40.0
    @{ Code = "C40.0.701"; Display = "Acromioclavicular joint"; Parent = "C40.0" }
    # C40.1
    @{ Code = "C40.1.310"; Display = "Metacarpal bone"; Parent = "C40.1" }
    @{ Code = "C40.1.311"; Display = "Finger bones"; Parent = "C40.1" }
    @{ Code = "C40.1.369"; Display = "Carpal bones"; Parent = "C40.1" }
    @{ Code = "C40.1.703"; Display = "Wrist joint"; Parent = "C40.1" }
    # C40.2
    @{ Code = "C40.2.471"; Display = "Patella"; Parent = "C40.2" }
    # C40.3
    @{ Code = "C40.3.410"; Display = "1. Metatarsal bone"; Parent = "C40.3" }
    @{ Code = "C40.3.419"; Display = "Metatarsal bone"; Parent = "C40.3" }
    @{ Code = "C40.3.420"; Display = "2. Metatarsal bone"; Parent = "C40.3" }
    @{ Code = "C40.3.430"; Display = "3. Metatarsal bone"; Parent = "C40.3" }
    @{ Code = "C40.3.440"; Display = "4. Metatarsal bone"; Parent = "C40.3" }
    @{ Code = "C40.3.450"; Display = "5. Metatarsal bone"; Parent = "C40.3" }
    @{ Code = "C40.3.411"; Display = "Phalanx of foot"; Parent = "C40.3" }
    @{ Code = "C40.3.460"; Display = "Talus"; Parent = "C40.3" }
    @{ Code = "C40.3.461"; Display = "Calcaneus"; Parent = "C40.3" }
    @{ Code = "C40.3.469"; Display = "Tarsal bones Fusswurzelknochen"; Parent = "C40.3" }
    @{ Code = "C40.3.871"; Display = "Ankle joint"; Parent = "C40.3" }
    # C41.0
    @{ Code = "C41.0.100"; Display = "Occipital bone"; Parent = "C41.0" }
    @{ Code = "C41.0.101"; Display = "Sphenoid/Clivus"; Parent = "C41.0" }
    @{ Code = "C41.0.102"; Display = "Temporal bone"; Parent = "C41.0" }
    @{ Code = "C41.0.106"; Display = "Frontal bone"; Parent = "C41.0" }
    @{ Code = "C41.0.107"; Display = "Parietal bone"; Parent = "C41.0" }
    @{ Code = "C41.0.119"; Display = "Nose NOS"; Parent = "C41.0" }
    @{ Code = "C41.0.120"; Display = "Maxillary sinus"; Parent = "C41.0" }
    # C41.1
    @{ Code = "C41.1.149"; Display = "Mandible"; Parent = "C41.1" }
    # C41.2 - cervical
    @{ Code = "C41.2.201"; Display = "Atlas"; Parent = "C41.2" }
    @{ Code = "C41.2.202"; Display = "Axix"; Parent = "C41.2" }
    @{ Code = "C41.2.203"; Display = "3. cervical vertebra"; Parent = "C41.2" }
    @{ Code = "C41.2.204"; Display = "4. cervical vertebra"; Parent = "C41.2" }
    @{ Code = "C41.2.205"; Display = "5.cervical vertebra"; Parent = "C41.2" }
    @{ Code = "C41.2.206"; Display = "6.cervical vertebra"; Parent = "C41.2" }
    @{ Code = "C41.2.207"; Display = "7.cervical vertebra"; Parent = "C41.2" }
    @{ Code = "C41.2.209"; Display = "Cervical vertebra NOS"; Parent = "C41.2" }
    # C41.2 - thoracic
    @{ Code = "C41.2.211"; Display = "1.thoracic vertebra"; Parent = "C41.2" }
    @{ Code = "C41.2.212"; Display = "2.thoracic vertebra"; Parent = "C41.2" }
    @{ Code = "C41.2.213"; Display = "3.thoracic vertebra"; Parent = "C41.2" }
    @{ Code = "C41.2.214"; Display = "4.thoracic vertebra"; Parent = "C41.2" }
    @{ Code = "C41.2.215"; Display = "5.thoracic vertebra"; Parent = "C41.2" }
    @{ Code = "C41.2.216"; Display = "6.thoracic vertebra"; Parent = "C41.2" }
    @{ Code = "C41.2.217"; Display = "7.thoracic vertebra"; Parent = "C41.2" }
    @{ Code = "C41.2.218"; Display = "8.thoracic vertebra"; Parent = "C41.2" }
    @{ Code = "C41.2.219"; Display = "9.thoracic vertebra"; Parent = "C41.2" }
    @{ Code = "C41.2.220"; Display = "10.thoracic vertebra"; Parent = "C41.2" }
    @{ Code = "C41.2.221"; Display = "11.thoracic vertebra"; Parent = "C41.2" }
    @{ Code = "C41.2.222"; Display = "12.thoracic vertebra"; Parent = "C41.2" }
    @{ Code = "C41.2.229"; Display = "Thoracic vertebra NOS"; Parent = "C41.2" }
    # C41.2 - lumbar (new)
    @{ Code = "C41.2.231"; Display = "1.lumbar vertebra"; Parent = "C41.2" }
    @{ Code = "C41.2.232"; Display = "2.lumbar vertebra"; Parent = "C41.2" }
    @{ Code = "C41.2.233"; Display = "3.lumbar vertebra"; Parent = "C41.2" }
    @{ Code = "C41.2.234"; Display = "4.lumbar vertebra"; Parent = "C41.2" }
    @{ Code = "C41.2.235"; Display = "5.lumbar vertebra"; Parent = "C41.2" }
    @{ Code = "C41.2.239"; Display = "Lumbar vertebra NOS"; Parent = "C41.2" }
    # C41.3
    @{ Code = "C41.3.301"; Display = "Clavicula"; Parent = "C41.3" }
    @{ Code = "C41.3.370"; Display = "Sternum"; Parent = "C41.3" }
    @{ Code = "C41.3.371"; Display = "Ribs"; Parent = "C41.3" }
    @{ Code = "C41.3.679"; Display = "Costovertebral joint"; Parent = "C41.3" }
    @{ Code = "C41.3.789"; Display = "Sternocostal joint"; Parent = "C41.3" }
    # C41.4
    @{ Code = "C41.4.400"; Display = "Ilium"; Parent = "C41.4" }
    @{ Code = "C41.4.401"; Display = "Acetabulum"; Parent = "C41.4" }
    @{ Code = "C41.4.402"; Display = "Pubic superior ramus"; Parent = "C41.4" }
    @{ Code = "C41.4.403"; Display = "Pubic inferior ramus"; Parent = "C41.4" }
    @{ Code = "C41.4.404"; Display = "Ischium"; Parent = "C41.4" }
    @{ Code = "C41.4.409"; Display = "Pelvis NOS"; Parent = "C41.4" }
    @{ Code = "C41.4.801"; Display = "Hip joint"; Parent = "C41.4" }
    @{ Code = "C41.4.802"; Display = "Symphysis pubis"; Parent = "C41.4" }
)

# ============================================================
# UPDATE VALUESET
# ============================================================
Write-Host "Updating ValueSet..."

# Fix C41.0.109 display from "Cranium" to "Calvarium"
$craniumEntry = $vs.expansion.contains | Where-Object { $_.code -eq "C41.0.109" }
if ($craniumEntry) {
    $craniumEntry.display = "Calvarium"
    Write-Host "  Updated C41.0.109 display: 'Cranium' -> 'Calvarium'"
}

# Fix C40.2.870 display from "Knee" to "Knee joint" (matching questionnaire)
$kneeEntry = $vs.expansion.contains | Where-Object { $_.code -eq "C40.2.870" }
if ($kneeEntry) {
    $kneeEntry.display = "Knee joint"
    Write-Host "  Updated C40.2.870 display: 'Knee' -> 'Knee joint'"
}

# Remove any existing C40.3.410 from ValueSet (it was "Metatarsal bone", now reassigned)
$existing410 = $vs.expansion.contains | Where-Object { $_.code -eq "C40.3.410" }
if ($existing410) {
    $vs.expansion.contains = @($vs.expansion.contains | Where-Object { $_.code -ne "C40.3.410" })
    Write-Host "  Removed old C40.3.410 entry from ValueSet (will re-add with correct display)"
}

# Build set of existing codes in ValueSet
$existingVSCodes = @{}
foreach ($entry in $vs.expansion.contains) {
    $existingVSCodes[$entry.code] = $true
}

# Add new codes to ValueSet
$added = 0
foreach ($nc in $newCodes) {
    if (-not $existingVSCodes.ContainsKey($nc.Code)) {
        $newEntry = [PSCustomObject]@{
            system  = "http://terminology.hl7.org/CodeSystem/icd-o-3"
            version = "3.2.ipath"
            code    = $nc.Code
            display = $nc.Display
        }
        $vs.expansion.contains += $newEntry
        $added++
    }
}

# Sort ValueSet contains by code (string sort)
$vs.expansion.contains = $vs.expansion.contains | Sort-Object code
Write-Host "  Added $added new entries to ValueSet. Total contains: $($vs.expansion.contains.Count)"

# Update expansion total
$vs.expansion.total = $vs.expansion.contains.Count
Write-Host "  Updated expansion.total to $($vs.expansion.total)"

# ============================================================
# UPDATE CODESYSTEM
# ============================================================
Write-Host "`nUpdating CodeSystem..."

# Build a lookup of existing concept codes
$existingCSCodes = @{}
$conceptIndex = @{}
for ($i = 0; $i -lt $cs.concept.Count; $i++) {
    $code = $cs.concept[$i].code
    $existingCSCodes[$code] = $i
    $conceptIndex[$code] = $i
}

# Group new codes by parent so we can update parent child properties
$parentsToUpdate = @{}
foreach ($nc in $newCodes) {
    if (-not $parentsToUpdate.ContainsKey($nc.Parent)) {
        $parentsToUpdate[$nc.Parent] = @()
    }
    $parentsToUpdate[$nc.Parent] += $nc.Code
}

# Add child properties to parent concepts
foreach ($parentCode in $parentsToUpdate.Keys) {
    if ($conceptIndex.ContainsKey($parentCode)) {
        $idx = $conceptIndex[$parentCode]
        $parentConcept = $cs.concept[$idx]
        
        # Get existing child codes
        $existingChildren = @{}
        foreach ($prop in $parentConcept.property) {
            if ($prop.code -eq "child") {
                $existingChildren[$prop.valueCode] = $true
            }
        }

        # Add missing child entries
        $childrenToAdd = $parentsToUpdate[$parentCode] | Where-Object { -not $existingChildren.ContainsKey($_) }
        foreach ($childCode in $childrenToAdd) {
            $newChildProp = [PSCustomObject]@{
                code      = "child"
                valueCode = $childCode
            }
            $parentConcept.property += $newChildProp
            Write-Host "  Added child '$childCode' to parent '$parentCode'"
        }

        # Sort properties: parent entries first, then child entries sorted by valueCode
        $parentProps = $parentConcept.property | Where-Object { $_.code -eq "parent" }
        $childProps = $parentConcept.property | Where-Object { $_.code -eq "child" } | Sort-Object valueCode
        $parentConcept.property = @($parentProps) + @($childProps)
    } else {
        Write-Warning "  Parent concept '$parentCode' not found in CodeSystem!"
    }
}

# Add new concept entries to CodeSystem
$addedCS = 0
foreach ($nc in $newCodes) {
    if (-not $existingCSCodes.ContainsKey($nc.Code)) {
        $newConcept = [PSCustomObject]@{
            code = $nc.Code
            property = @(
                [PSCustomObject]@{
                    code      = "parent"
                    valueCode = $nc.Parent
                }
            )
        }
        $cs.concept += $newConcept
        $addedCS++
    }
}

# Sort concepts by code
$cs.concept = $cs.concept | Sort-Object code
Write-Host "  Added $addedCS new concepts to CodeSystem. Total concepts: $($cs.concept.Count)"

# Update count
$cs.count = $cs.concept.Count
Write-Host "  Updated CodeSystem count to $($cs.count)"

# ============================================================
# WRITE FILES
# ============================================================
Write-Host "`nWriting files..."

# Use depth to prevent truncated JSON
$jsonOptions = @{
    Depth = 10
}

$cs | ConvertTo-Json @jsonOptions | Set-Content $csPath -Encoding UTF8
Write-Host "  Written: $csPath"

$vs | ConvertTo-Json @jsonOptions | Set-Content $vsPath -Encoding UTF8
Write-Host "  Written: $vsPath"

Write-Host "`nDone!"
