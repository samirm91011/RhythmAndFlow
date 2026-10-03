#!/usr/bin/env bash
# One-time setup for a fresh Ubuntu 24.04 server (xneelo Cloud). Run as root:
#     sudo bash setup-server.sh
# What it does: updates the system, installs Docker, firewall (only SSH/HTTP/HTTPS), brute-force protection,
# automatic security updates, a swap file, a "rfdeploy" user for the pipeline, nightly database backups.
set -euo pipefail

if [ "$(id -u)" -ne 0 ]; then echo "Run as root: sudo bash setup-server.sh"; exit 1; fi

APP_DIR=/opt/rhythmflow
DEPLOY_USER=rfdeploy

echo "==> 1/9  Updating the system"
export DEBIAN_FRONTEND=noninteractive
apt-get update -y
apt-get upgrade -y
apt-get install -y ca-certificates curl gnupg ufw fail2ban unattended-upgrades jq apache2-utils

echo "==> 2/9  Time zone"
timedatectl set-timezone Africa/Johannesburg || true

echo "==> 3/9  Swap file (safety net for a 2 GB server)"
if ! swapon --show | grep -q '/swapfile'; then
  fallocate -l 2G /swapfile
  chmod 600 /swapfile
  mkswap /swapfile
  swapon /swapfile
  grep -q '/swapfile' /etc/fstab || echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi
sysctl -w vm.swappiness=10 >/dev/null
grep -q 'vm.swappiness' /etc/sysctl.d/99-rhythmflow.conf 2>/dev/null || echo 'vm.swappiness=10' > /etc/sysctl.d/99-rhythmflow.conf

echo "==> 4/9  Docker"
if ! command -v docker >/dev/null 2>&1; then
  install -m 0755 -d /etc/apt/keyrings
  curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
  chmod a+r /etc/apt/keyrings/docker.asc
  echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" > /etc/apt/sources.list.d/docker.list
  apt-get update -y
  apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
fi
systemctl enable --now docker
# Keep container logs from filling the disk.
cat > /etc/docker/daemon.json <<'EOF'
{ "log-driver": "json-file", "log-opts": { "max-size": "10m", "max-file": "3" } }
EOF
systemctl restart docker

echo "==> 5/9  Firewall (only SSH, HTTP, HTTPS)"
ufw default deny incoming
ufw default allow outgoing
ufw allow 22/tcp
ufw allow 80/tcp
ufw allow 443/tcp
ufw --force enable

echo "==> 6/9  Brute-force protection and automatic security updates"
systemctl enable --now fail2ban
dpkg-reconfigure -f noninteractive unattended-upgrades

echo "==> 7/9  Deploy user ($DEPLOY_USER) for the GitHub pipeline"
id "$DEPLOY_USER" >/dev/null 2>&1 || adduser --disabled-password --gecos "" "$DEPLOY_USER"
usermod -aG docker "$DEPLOY_USER"
mkdir -p "/home/$DEPLOY_USER/.ssh" "$APP_DIR/backups"
if [ -s /root/.ssh/authorized_keys ]; then
  cp /root/.ssh/authorized_keys "/home/$DEPLOY_USER/.ssh/authorized_keys"
fi
chown -R "$DEPLOY_USER:$DEPLOY_USER" "/home/$DEPLOY_USER/.ssh" "$APP_DIR"
chmod 700 "/home/$DEPLOY_USER/.ssh"
chmod 600 "/home/$DEPLOY_USER/.ssh/authorized_keys" 2>/dev/null || true

echo "==> 8/9  SSH hardening (only if a key is installed, so you cannot lock yourself out)"
if [ -s /root/.ssh/authorized_keys ] || [ -s "/home/$DEPLOY_USER/.ssh/authorized_keys" ]; then
  mkdir -p /etc/ssh/sshd_config.d
  cat > /etc/ssh/sshd_config.d/99-rhythmflow.conf <<'EOF'
PasswordAuthentication no
PermitRootLogin prohibit-password
EOF
  systemctl reload ssh || systemctl reload sshd || true
else
  echo "    (no SSH key found - password login left on. Add a key, then re-run this script.)"
fi

echo "==> 9/9  Nightly database backup (02:30, keeps 7 days)"
if [ -f "$APP_DIR/backup.sh" ]; then
  cat > /etc/cron.d/rhythmflow-backup <<EOF
30 2 * * * $DEPLOY_USER cd $APP_DIR && ./backup.sh >> $APP_DIR/backups/backup.log 2>&1
EOF
  chmod 644 /etc/cron.d/rhythmflow-backup
else
  echo "    (backup.sh is not here yet - it is copied by the first deployment; re-run this script afterwards)"
fi

echo
echo "Server is ready."
echo "Next: as $DEPLOY_USER, create $APP_DIR/.env from deploy/.env.example, then run the 'Deploy' workflow (docs/XNEELO-SETUP.md)."
