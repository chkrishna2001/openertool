#!/bin/bash
set -e

echo "Starting Integration Tests..."

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
