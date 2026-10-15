#!/usr/bin/env python3
"""Generates the offline Steam test double: an x86-64 libsteam_api.so exporting every entry
point Steamworks.NET binds (src/ThirdParty/Steamworks.NET), for runs on machines without a
logged-in Steam client. TEST INFRASTRUCTURE ONLY: the product loads the game's own
libsteam_api.so; this library is used only when a run passes
`--native-override steam_api=<path>` (scripts/run-offline.sh).

Every function returns zero (false, null, invalid handle, empty count) except those in
VALUES, which describe a logged-in Steam user who owns the base game, has no DLC and no
Workshop subscriptions, with English as the language. No callback is ever delivered."""
import re, sys

STEAM_ID = 0x0110000100000001  # individual account 1, public universe
APP_ID = 457140

# name -> ("int", value) | ("str", text) | ("ptr",) | ("asm", body)
VALUES = {
    "SteamAPI_Init": ("int", 1),
    "SteamAPI_IsSteamRunning": ("int", 1),
    "SteamAPI_GetHSteamPipe": ("int", 1),
    "SteamAPI_GetHSteamUser": ("int", 1),
    "SteamAPI_GetSteamInstallPath": ("str", ""),
    "SteamInternal_CreateInterface": ("ptr",),
    "SteamAPI_ISteamUser_BLoggedOn": ("int", 1),
    "SteamAPI_ISteamUser_GetSteamID": ("int", STEAM_ID),
    "SteamAPI_ISteamUser_GetPlayerSteamLevel": ("int", 1),
    "SteamAPI_ISteamFriends_GetPersonaName": ("str", "Player"),
    "SteamAPI_ISteamFriends_GetFriendPersonaName": ("str", ""),
    "SteamAPI_ISteamApps_BIsSubscribed": ("int", 1),
    # (self in rdi, appID in esi): owned only for the base game.
    "SteamAPI_ISteamApps_BIsSubscribedApp": ("asm", f"xor eax, eax\n  cmp esi, {APP_ID}\n  sete al"),
    # (self, steamID, appID in edx): k_EUserHasLicenseResultHasLicense (0) for the base game,
    # k_EUserHasLicenseResultDoesNotHaveLicense (1) for anything else (the DLCs).
    "SteamAPI_ISteamUser_UserHasLicenseForApp": ("asm", f"xor eax, eax\n  cmp edx, {APP_ID}\n  setne al"),
    "SteamAPI_ISteamApps_GetCurrentGameLanguage": ("str", "english"),
    "SteamAPI_ISteamApps_GetAvailableGameLanguages": ("str", "english"),
    "SteamAPI_ISteamApps_GetAppOwner": ("int", STEAM_ID),
    "SteamAPI_ISteamApps_GetEarliestPurchaseUnixTime": ("int", 1500000000),
    "SteamAPI_ISteamApps_GetAppBuildId": ("int", 1),
    "SteamAPI_ISteamUtils_GetAppID": ("int", APP_ID),
    "SteamAPI_ISteamUtils_GetSteamUILanguage": ("str", "english"),
    "SteamAPI_ISteamUtils_GetIPCountry": ("str", "US"),
    "SteamAPI_ISteamUtils_GetServerRealTime": ("int", 1700000000),
    "SteamAPI_ISteamUGC_CreateQueryUserUGCRequest": ("int", 0xFFFFFFFFFFFFFFFF),
    "SteamAPI_ISteamUGC_CreateQueryAllUGCRequestPage": ("int", 0xFFFFFFFFFFFFFFFF),
    "SteamAPI_ISteamUGC_CreateQueryAllUGCRequestCursor": ("int", 0xFFFFFFFFFFFFFFFF),
    "SteamAPI_ISteamUGC_CreateQueryUGCDetailsRequest": ("int", 0xFFFFFFFFFFFFFFFF),
}
# Interface accessors: a non-null dummy object (calls only pass it back).
POINTER_PATTERNS = [re.compile(r"^SteamAPI_ISteamClient_GetISteam"), re.compile(r"^SteamAPI_Steam[A-Za-z]+_v\d+$"),
                    re.compile(r"^SteamInternal_FindOrCreate")]

def entry_points(path):
    names = re.findall(r'EntryPoint\s*=\s*"([A-Za-z0-9_]+)"', open(path, encoding="utf-8").read())
    return sorted(set(names))

def main(native_methods, out):
    lines = [".intel_syntax noprefix", ".text"]
    strings = {}
    for name in entry_points(native_methods):
        v = VALUES.get(name)
        if v is None and any(p.match(name) for p in POINTER_PATTERNS): v = ("ptr",)
        if v is None: body = "xor eax, eax"
        elif v[0] == "int": body = f"mov rax, {v[1]:#x}"
        elif v[0] == "ptr": body = "lea rax, [rip + stub_object]"
        elif v[0] == "str":
            label = strings.setdefault(v[1], f"stub_str{len(strings)}")
            body = f"lea rax, [rip + {label}]"
        else: body = v[1]
        lines += [f".globl {name}", f".type {name}, @function", f"{name}:", f"  {body}", "  pxor xmm0, xmm0", "  ret"]
    lines += [".section .rodata"]
    for text, label in strings.items():
        lines += [f"{label}:", f'  .asciz "{text}"']
    lines += [".bss", ".balign 64", "stub_object:", "  .zero 4096", '.section .note.GNU-stack,"",@progbits']
    open(out, "w").write("\n".join(lines) + "\n")

if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
