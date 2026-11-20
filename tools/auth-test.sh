#!/usr/bin/env bash
# Auth-page validation probes against a running SMS instance.
# Usage: bash tools/auth-test.sh [base-url]
set -u
B="${1:-http://localhost:49915}"
UA="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
J=/tmp/cj_auth_probe.txt
ARGS=()

# curl --data-urlencode "name@file" keeps base64 ViewState away from the
# MSYS path converter (a leading "/" would otherwise become "E:/Git/...").
mkdir -p build/tmp
add() {
  local tf="build/tmp/"+""
  tf="build/tmp/f_${RANDOM}${RANDOM}.txt"
  printf '%s' "$2" > "$tf"
  ARGS+=(--data-urlencode "$1@$tf")
}

field() { grep -oE "id=\"$1\"[^>]*value=\"[^\"]*\"" "$2" | head -1 | sed -E 's/.*value="([^"]*)".*/\1/'; }

# visible validator spans only (ASP.NET hides valid ones with display:none)
visible() {
  grep -oE '<span id="MainContent_[a-zA-Z]+"[^>]*>' "$1" \
    | grep -v 'display:none' \
    | sed -E 's/.*id="MainContent_([a-zA-Z]+)".*/\1/' | tr '\n' ' '
}

alerts() { grep -oE "alert alert-[a-z]+[^>]*>[^<]*" "$1" | sed -E "s/.*>//" | tr '\n' '|'; }

get_page() { # page outfile
  rm -f "$J"
  curl -s -A "$UA" -c "$J" "$B/Account/$1" -o "$2"
}

open_form() { # page outfile
  get_page "$1" "$2"
  ARGS=(
    --data-urlencode "__VIEWSTATE@$(save __VIEWSTATE "$2")"
  )
}

save() { # id file -> temp path (relative so native curl can open it)
  local tf="build/tmp/s_${RANDOM}${RANDOM}.txt"
  field "$1" "$2" > "$tf"; echo "$tf"
}

post() { # page outfile extra-name=value ...
  local page="$1" out="$2"; shift 2
  ARGS=()
  add "__VIEWSTATE" "$(field __VIEWSTATE /tmp/form.html)"
  add "__VIEWSTATEGENERATOR" "$(field __VIEWSTATEGENERATOR /tmp/form.html)"
  add "__EVENTVALIDATION" "$(field __EVENTVALIDATION /tmp/form.html)"
  local kv
  for kv in "$@"; do add "${kv%%=*}" "${kv#*=}"; done
  curl -s -A "$UA" -b "$J" -c "$J" -o "$out" -w "  POST %{http_code} %{redirect_url}\n" \
    "${ARGS[@]}" "$B/Account/$page"
}

echo "== 1. login initial =="
get_page Login.aspx /tmp/form.html
echo "  visible validators: [$(visible /tmp/form.html)]"
echo "  copy: $(grep -oE 'auth-sub\">[^<]*' /tmp/form.html | sed 's/auth-sub">//')"
echo "  email label: $(grep -oE 'for=\"MainContent_txtEmail\"[^>]*>[^<]*' /tmp/form.html | sed 's/.*>//')"

echo "== 2. login empty =="
post Login.aspx /tmp/r.html "ctl00\$MainContent\$txtEmail=" "ctl00\$MainContent\$txtPassword=" "ctl00\$MainContent\$btnSignIn=Sign in"
echo "  visible: [$(visible /tmp/r.html)]"

echo "== 3. login invalid email =="
get_page Login.aspx /tmp/form.html
post Login.aspx /tmp/r.html "ctl00\$MainContent\$txtEmail=not-an-email" "ctl00\$MainContent\$txtPassword=whatever" "ctl00\$MainContent\$btnSignIn=Sign in"
echo "  visible: [$(visible /tmp/r.html)]"

echo "== 4. login wrong password =="
get_page Login.aspx /tmp/form.html
post Login.aspx /tmp/r.html "ctl00\$MainContent\$txtEmail=student@sms.edu" "ctl00\$MainContent\$txtPassword=WrongPass@1" "ctl00\$MainContent\$btnSignIn=Sign in"
echo "  visible: [$(visible /tmp/r.html)] alerts: [$(alerts /tmp/r.html)]"

echo "== 5. login unknown account =="
get_page Login.aspx /tmp/form.html
post Login.aspx /tmp/r.html "ctl00\$MainContent\$txtEmail=nobody.here@gmail.com" "ctl00\$MainContent\$txtPassword=Secret@123" "ctl00\$MainContent\$btnSignIn=Sign in"
echo "  visible: [$(visible /tmp/r.html)] alerts: [$(alerts /tmp/r.html)]"

for role in admin faculty student; do
  echo "== 6.$role seed login =="
  get_page Login.aspx /tmp/form.html
  post Login.aspx /tmp/r.html "ctl00\$MainContent\$txtEmail=$role@sms.edu" \
      "ctl00\$MainContent\$txtPassword=$(case $role in admin) echo Admin@123;; faculty) echo Faculty@123;; *) echo Student@123;; esac)" \
      "ctl00\$MainContent\$btnSignIn=Sign in"
done

echo "== 7. signup initial =="
get_page Signup.aspx /tmp/form.html
echo "  visible: [$(visible /tmp/form.html)]"
echo "  copy: $(grep -oE 'auth-sub\">[^<]*' /tmp/form.html | sed 's/auth-sub">//')"

echo "== 8. signup empty =="
post Signup.aspx /tmp/r.html "ctl00\$MainContent\$txtFullName=" "ctl00\$MainContent\$txtEmail=" \
  "ctl00\$MainContent\$txtPhone=" "ctl00\$MainContent\$txtPassword=" "ctl00\$MainContent\$txtConfirm=" \
  "ctl00\$MainContent\$btnCreate=Create account"
echo "  visible: [$(visible /tmp/r.html)]"

echo "== 9. signup password mismatch =="
get_page Signup.aspx /tmp/form.html
post Signup.aspx /tmp/r.html "ctl00\$MainContent\$txtFullName=Test Person" "ctl00\$MainContent\$txtEmail=test.person@gmail.com" \
  "ctl00\$MainContent\$txtPhone=" "ctl00\$MainContent\$txtPassword=Strong@1234" "ctl00\$MainContent\$txtConfirm=Different@1234" \
  "ctl00\$MainContent\$btnCreate=Create account"
echo "  visible: [$(visible /tmp/r.html)]"

echo "== 10. signup bad phone =="
get_page Signup.aspx /tmp/form.html
post Signup.aspx /tmp/r.html "ctl00\$MainContent\$txtFullName=Test Person" "ctl00\$MainContent\$txtEmail=test.person@gmail.com" \
  "ctl00\$MainContent\$txtPhone=123" "ctl00\$MainContent\$txtPassword=Strong@1234" "ctl00\$MainContent\$txtConfirm=Strong@1234" \
  "ctl00\$MainContent\$btnCreate=Create account"
echo "  visible: [$(visible /tmp/r.html)]"

echo "== 11. forgot initial =="
get_page Forgot.aspx /tmp/form.html
echo "  visible: [$(visible /tmp/form.html)]"

echo "== 12. forgot empty =="
post Forgot.aspx /tmp/r.html "ctl00\$MainContent\$txtEmail=" "ctl00\$MainContent\$btnSend=Email me the reset link"
echo "  visible: [$(visible /tmp/r.html)]"

echo "== 13. forgot invalid =="
get_page Forgot.aspx /tmp/form.html
post Forgot.aspx /tmp/r.html "ctl00\$MainContent\$txtEmail=nope" "ctl00\$MainContent\$btnSend=Email me the reset link"
echo "  visible: [$(visible /tmp/r.html)]"

echo "== 14. forgot unknown account =="
get_page Forgot.aspx /tmp/form.html
post Forgot.aspx /tmp/r.html "ctl00\$MainContent\$txtEmail=ghost.user@gmail.com" "ctl00\$MainContent\$btnSend=Email me the reset link"
echo "  visible: [$(visible /tmp/r.html)] alerts: [$(alerts /tmp/r.html)]"
