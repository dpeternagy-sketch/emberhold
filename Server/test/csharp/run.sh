#!/bin/sh
# Compiles the client online code against tiny Unity stand-ins and runs the checks (needs mono: mcs + mono).
set -e
D=$(cd "$(dirname "$0")" && pwd); S="$D/../../../Assets/Emberhold/Scripts"; OUT=$(mktemp -d)
mcs -out:"$OUT/t.exe" "$D/UnityStub.cs" "$D/OnlineTests.cs" "$S/Data/GameState.cs" "$S/Data/BuildingCatalog.cs" "$S/Army/UnitDef.cs" "$S"/Online/*.cs
mcs -target:library -define:EMBERHOLD_UGS -out:"$OUT/ugs.dll" "$D/UnityStub.cs" "$D/UgsStub.cs" "$S/Data/GameState.cs" "$S/Data/BuildingCatalog.cs" "$S/Army/UnitDef.cs" "$S"/Online/*.cs
mono "$OUT/t.exe"
