#!/bin/bash
set -e

echo "Starting Integration Tests..."

SANDBOX_ROOT="$(mktemp -d -t opener-integration-XXXXXX)"
export HOME="$SANDBOX_ROOT/home"
export XDG_CONFIG_HOME="$SANDBOX_ROOT/config"
export XDG_DATA_HOME="$SANDBOX_ROOT/data"
export OPENER_HOME="$HOME"
export OPENER_DATA_DIR="$SANDBOX_ROOT/data/Opener"

mkdir -p "$HOME" "$XDG_CONFIG_HOME" "$XDG_DATA_HOME" "$OPENER_DATA_DIR"

cleanup() {
	rm -rf "$SANDBOX_ROOT"
}

trap cleanup EXIT

# 1. Setup portable mode with password
mkdir -p ~/.opener
echo "{\"encryptionMode\":\"portable\"}" > ~/.opener/config.json
# FileCredentialService uses .opener/.internal_pass
echo "testpassword123" > ~/.opener/.internal_pass

# 2. Add some keys
o add myweb "https://google.com?q={0}" -t WebPath
o add mydata "secret123" -t Data
o add myjson "{\"id\":1}" -t JsonData

# 3. List keys
echo "Listing keys..."
o list

# 4. Verify we can get a key (using subshell/capture if needed, but for now just check it doesn't crash)
o mydata

# 5. Test Export
o export backup.dat <<EOF
exportpass
exportpass
EOF

# 6. Test Import (Clean current data first)
rm -f ~/.opener/opener.dat
o list # Should be empty
o import backup.dat <<EOF
exportpass
EOF

# 7. Final check
o list | grep "myweb"

echo "Integration Tests Passed!"
