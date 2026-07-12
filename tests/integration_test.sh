#!/bin/bash
set -e
set -o pipefail

echo "Starting Integration Tests..."

SANDBOX_ROOT="$(mktemp -d -t opener-integration-XXXXXX)"
export HOME="$SANDBOX_ROOT/home"
export XDG_CONFIG_HOME="$SANDBOX_ROOT/config"
export XDG_DATA_HOME="$SANDBOX_ROOT/data"
export OPENER_HOME="$HOME"
export OPENER_DATA_DIR="$SANDBOX_ROOT/data"
# GetDataFilePath() always appends an "Opener" folder under OPENER_DATA_DIR.
DATA_FILE="$OPENER_DATA_DIR/Opener/opener.dat"

mkdir -p "$HOME" "$XDG_CONFIG_HOME" "$XDG_DATA_HOME" "$OPENER_DATA_DIR"

cleanup() {
	rm -rf "$SANDBOX_ROOT"
}

trap cleanup EXIT

# 1. Setup portable mode with password via the CLI (not by hand-seeding credential files -
# the fallback credential file is encrypted now, so writing it directly no longer works,
# and this is also more representative of how a real user sets this up). Interactive
# prompts don't reliably read from redirected/piped stdin on this binary, and a crashed
# prompt still exits 0 (System.CommandLine's exception handler swallows it), so this uses
# -y/--password to skip prompting entirely rather than trying to feed it input, and the
# result is verified explicitly below instead of trusting the exit code.
o config set-encryption portable --password testpassword123 -y
mode_check=$(o config show)
if [[ "$mode_check" != *"portable"* ]]; then
	echo "set-encryption did not switch to portable mode. 'o config show' output was:"
	echo "$mode_check"
	exit 1
fi

# 2. Add some keys
o add myweb "https://google.com?q={0}" -t WebPath

# Confirm the password is actually retrievable in a fresh process, not just that
# config.json's static field flipped - `list` requires decrypting the vault, which
# requires GetPassword() to actually succeed. A credential-store bug (falling back to
# file storage on SetPassword but never checking that fallback on a later GetPassword)
# silently broke exactly this: config show still said "portable" while every subsequent
# command failed to find the password at all.
list_after_migration=$(o list 2>&1)
if [[ "$list_after_migration" != *"myweb"* ]]; then
	echo "Vault unreadable after migrating to portable mode. 'o list' output was:"
	echo "$list_after_migration"
	exit 1
fi
o add mydata "secret123" -t Data
o add myjson "{\"id\":1}" -t JsonData

# 3. List keys
echo "Listing keys..."
o list

# 4. Verify we can get a key (using subshell/capture if needed, but for now just check it doesn't crash)
o mydata

# 5. TOTP: add a known RFC 6238 test secret and confirm a 6-digit code comes back
echo "Testing TOTP..."
o add mytotp GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ -t Totp
totp_code=$(o mytotp -r)
if ! [[ "$totp_code" =~ ^[0-9]{6}$ ]]; then
	echo "TOTP code '$totp_code' is not a 6-digit number."
	exit 1
fi
echo "Success: TOTP produced a 6-digit code."

# 6. Non-interactive picker fallback: bare `o` with keys present must not hang and must
# print a static list instead of an interactive prompt.
echo "Testing non-interactive picker fallback..."
picker_out=$(o < /dev/null)
if [[ "$picker_out" != *"myweb"* ]]; then
	echo "Bare 'o' did not fall back to a static list. Output was:"
	echo "$picker_out"
	exit 1
fi
echo "Success: bare 'o' fell back to a static list without hanging."

# 7. REST chaining: structural smoke test. Live external HTTP calls are flaky/undesirable
# in a CI integration test, so this only confirms a chained key's steps survive round-trip.
echo "Testing REST chain storage..."
o add mychain '{"steps":[{"url":"https://example.com/login","method":"POST","extract":{"token":"access_token"}},{"url":"https://example.com/data","headers":{"Authorization":"Bearer {{token}}"}}]}' -t Rest
chain_view=$(o view mychain)
if [[ "$chain_view" != *"steps"* ]] || [[ "$chain_view" != *"access_token"* ]]; then
	echo "REST chain was not stored correctly. Output was:"
	echo "$chain_view"
	exit 1
fi
echo "Success: REST chain stored correctly."

# 8. Git sync: push to a throwaway local bare remote, wipe local data, pull it back, and
# confirm a key reappears.
echo "Testing git sync..."
REMOTE_DIR="$SANDBOX_ROOT/remote.git"
git init --bare "$REMOTE_DIR" > /dev/null
o config set-sync-remote "$REMOTE_DIR"
o sync push
rm -f "$DATA_FILE"
o sync pull
o list | grep "myweb"
echo "Success: git sync round-tripped a key."

# 9. Test Export (--password avoids the interactive prompt, same reasoning as step 1)
o export backup.dat --password exportpass
if [ ! -f backup.dat ]; then
	echo "Export did not create backup.dat"
	exit 1
fi

# 10. Test Import (Clean current data first)
rm -f "$DATA_FILE"
o list # Should be empty
o import backup.dat --password exportpass

# 11. Final check
o list | grep "myweb"

echo "Integration Tests Passed!"
