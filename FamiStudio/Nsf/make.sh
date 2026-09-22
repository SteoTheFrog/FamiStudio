#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

CA65="../../Tools/ca65.exe"
LD65="../../Tools/ld65.exe"

PATCHTEXT="PatchText"
PRINTCODESIZE="PrintCodeSize"
EXTRACTNOTETABLE="ExtractNoteTable"

run_helper()
{
    local name="$1"
    shift

    if command -v "$name" >/dev/null 2>&1; then
        "$name" "$@"
        return
    fi

    if [[ -f "${name}.exe" ]]; then
        wine "${name}.exe" "$@"
        return
    fi

    echo "Cannot find helper: $name" >&2
    exit 1
}

compile()
{
    local cfg="$1"
    local patch="$2"
    local defines="$3"
    local output="$4"

    echo "$output"

    run_helper "$PATCHTEXT" "$cfg" tmp.cfg "$patch"

    read -r -a define_args <<< "$defines"

wine "$CA65" nsf.s \
    -g \
    -o "${output}.o" \
    "${define_args[@]}"

wine "$LD65" \
    -C tmp.cfg \
    -o "${output}.bin" \
    "${output}.o" \
    --mapfile "${output}.map" \
    --dbgfile "${output}.dbg"

    run_helper "$PRINTCODESIZE" \
        "${output}.bin" \
        "$patch"

    run_helper "$EXTRACTNOTETABLE" \
        "${output}.dbg" \
        "$patch"

    rm -f tmp.cfg
}

rm -f ./*.o ./*.bin ./*.dbg ./*.map

while IFS= read -r line; do
    [[ "$line" =~ ^[[:space:]]*CALL[[:space:]]+:CompileNsfPermutation ]] || continue

    args="${line#*CompileNsfPermutation }"

    IFS=',' read -r cfg patch defines output <<< "$args"

    cfg="${cfg#"${cfg%%[![:space:]]*}"}"
    cfg="${cfg%"${cfg##*[![:space:]]}"}"

    patch="${patch#"${patch%%[![:space:]]*}"}"
    patch="${patch%"${patch##*[![:space:]]}"}"

    defines="${defines#"${defines%%[![:space:]]*}"}"
    defines="${defines%"${defines##*[![:space:]]}"}"

    output="${output#"${output%%[![:space:]]*}"}"
    output="${output%"${output##*[![:space:]]}"}"

    patch="${patch#\"}"
    patch="${patch%\"}"

    defines="${defines#\"}"
    defines="${defines%\"}"

    compile "$cfg" "$patch" "$defines" "$output"

done < make.bat
