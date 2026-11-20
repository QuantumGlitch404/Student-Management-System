#!/usr/bin/env bash
# Renders every page as its role, follows the redirects the app performs by
# design, and checks that the typography layer is linked on each render.
# Usage: bash tools/page-check.sh [base-url]
set -u
B="${1:-http://localhost:49915}"
UA="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
mkdir -p build/tmp

login() { # role email password
  local role="$1" email="$2" pass="$3" jar="/tmp/pc_$1.txt"
  rm -f "$jar"
  curl -s -A "$UA" -c "$jar" "$B/Account/Login.aspx" -o /tmp/pc_login.html
  local k v
  for k in __VIEWSTATE __VIEWSTATEGENERATOR __EVENTVALIDATION; do
    v=$(grep -oE "id=\"$k\"[^>]*value=\"[^\"]*\"" /tmp/pc_login.html | head -1 | sed -E 's/.*value="([^"]*)".*/\1/')
    printf '%s' "$v" > "build/tmp/pc_$k.txt"
  done
  local code
  code=$(curl -s -A "$UA" -b "$jar" -c "$jar" -o /dev/null -w "%{http_code}" \
    --data-urlencode "__VIEWSTATE@build/tmp/pc___VIEWSTATE.txt" \
    --data-urlencode "__VIEWSTATEGENERATOR@build/tmp/pc___VIEWSTATEGENERATOR.txt" \
    --data-urlencode "__EVENTVALIDATION@build/tmp/pc___EVENTVALIDATION.txt" \
    --data-urlencode "ctl00\$MainContent\$txtEmail=$email" \
    --data-urlencode "ctl00\$MainContent\$txtPassword=$pass" \
    --data-urlencode "ctl00\$MainContent\$btnSignIn=Sign in" \
    "$B/Account/Login.aspx")
  echo "login $role: $code"
}

fail=0; total=0
render() { # path jar -> sets CODE, LOC, BODY
  BODY=/tmp/pc_body.html
  read -r CODE LOC <<<"$(curl -s -A "$UA" -b "$2" -o "$BODY" -w "%{http_code} %{redirect_url}" "$B$1")"
}
has_typo() { grep -q "/Content/css/typography.css" "$BODY"; }
ok() { total=$((total+1)); }
bad() { total=$((total+1)); fail=$((fail+1)); echo "  FAIL $*"; }

check() { # path jar [expected-redirect-regex]
  local path="$1" jar="$2" allow="${3:-}"
  render "$path" "$jar"
  if [ "$CODE" = "200" ]; then
    ok
    case "$path" in /Account/*) ;; *) has_typo || bad "$path missing typography.css" ;; esac
  elif [ "$CODE" = "302" ] && [ -n "$allow" ] && [[ "$LOC" =~ $allow ]]; then
    local target="${LOC#"$B"}"
    render "$target" "$jar"
    if [ "$CODE" = "200" ] && has_typo; then ok; echo "  ok   $path -> 302 $target"
    else bad "$path redirect target $target -> $CODE"; fi
  else
    bad "$path -> $CODE $LOC"
  fi
}

login admin admin@sms.edu Admin@123
login faculty faculty@sms.edu Faculty@123
login student student@sms.edu Student@123

echo "== account pages =="
check /Account/Login.aspx /tmp/pc_anon.txt
check /Account/Signup.aspx /tmp/pc_anon.txt
check /Account/Forgot.aspx /tmp/pc_anon.txt
check /Account/Error.aspx /tmp/pc_anon.txt
check /Account/Reset.aspx /tmp/pc_anon.txt
check /Account/Verify.aspx /tmp/pc_anon.txt "Login\.aspx"   # no pending OTP in session

echo "== admin ($(cd StudentManagementSystem/Admin && ls *.aspx | wc -l) pages) =="
for f in $(cd StudentManagementSystem/Admin && ls *.aspx); do check "/Admin/$f" /tmp/pc_admin.txt; done

echo "== faculty ($(cd StudentManagementSystem/Faculty && ls *.aspx | wc -l) pages) =="
for f in $(cd StudentManagementSystem/Faculty && ls *.aspx); do check "/Faculty/$f" /tmp/pc_faculty.txt; done

echo "== student ($(cd StudentManagementSystem/Student && ls *.aspx | wc -l) pages) =="
for f in $(cd StudentManagementSystem/Student && ls *.aspx); do
  if [ "$f" = "PayCheckout.aspx" ]; then
    check "/Student/$f" /tmp/pc_student.txt "Fees\.aspx"   # checkout needs a fee id
  else
    check "/Student/$f" /tmp/pc_student.txt
  fi
done

echo "== shared =="
check /Default.aspx /tmp/pc_student.txt "Index\.aspx"       # role-aware landing redirect
check /Student/Notifications.aspx /tmp/pc_student.txt
check /Notifications/Index.aspx /tmp/pc_student.txt
check /Notifications/Index.aspx /tmp/pc_faculty.txt
check /Notifications/Index.aspx /tmp/pc_admin.txt
check /Account/Settings.aspx /tmp/pc_student.txt
check /Account/Settings.aspx /tmp/pc_faculty.txt
check /Admin/Notifications.aspx /tmp/pc_admin.txt
check /Admin/Settings.aspx /tmp/pc_admin.txt
render "/" /tmp/pc_anon.txt
if [ "$CODE" = "302" ] && [[ "$LOC" =~ Login\.aspx ]]; then ok; echo "  ok   / -> 302 Login"
else bad "root -> $CODE $LOC"; fi

echo "---- $total pages checked, $fail failures ----"
exit $([ "$fail" -eq 0 ] && echo 0 || echo 1)
