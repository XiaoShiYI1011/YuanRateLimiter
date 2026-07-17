#!/usr/bin/env bash
set -euo pipefail

ROLE="app-3"
PUBLIC_IP="${PUBLIC_IP:-REDACTED_PUBLIC_IP}"
PRIVATE_IP="${PRIVATE_IP:-REDACTED_PRIVATE_IP}"
APP_PORT="${APP_PORT:-5001}"
REDIS_HOST="${REDIS_HOST:?please set REDIS_HOST before running this script}"
REDIS_PORT="${REDIS_PORT:-6379}"
REDIS_PASSWORD="${REDIS_PASSWORD:?please set REDIS_PASSWORD before running this script}"

export DEBIAN_FRONTEND=noninteractive

log() {
  echo "[$(date +'%F %T')] [$ROLE] $*"
}

require_sudo() {
  sudo -v
}

install_common_packages() {
  log "Installing common packages"
  sudo apt-get update
  sudo apt-get install -y \
    ca-certificates curl wget gnupg lsb-release software-properties-common \
    unzip git jq htop net-tools iproute2 sysstat iftop iotop tar vim \
    prometheus-node-exporter

  sudo timedatectl set-timezone Asia/Shanghai || true
  sudo systemctl enable --now sysstat || true
  sudo systemctl enable --now prometheus-node-exporter || true
}

tune_host() {
  log "Applying app-node kernel limits"
  sudo tee /etc/sysctl.d/99-yuan-rate-limiter.conf >/dev/null <<'SYSCTL'
net.core.somaxconn = 4096
net.ipv4.ip_local_port_range = 1024 65535
net.ipv4.tcp_tw_reuse = 1
fs.file-max = 1048576
SYSCTL
  sudo sysctl --system >/dev/null || true

  sudo tee /etc/security/limits.d/99-yuan-rate-limiter.conf >/dev/null <<'LIMITS'
* soft nofile 1048576
* hard nofile 1048576
root soft nofile 1048576
root hard nofile 1048576
LIMITS
}

install_dotnet() {
  if command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks | grep -q '^6\.'; then
    log ".NET 6 SDK already installed"
    dotnet --info
    return
  fi

  log "Installing .NET 6 SDK from Ubuntu feed"
  sudo add-apt-repository -y universe
  sudo apt-get update
  sudo apt-get install -y dotnet-sdk-6.0
}

prepare_app_dirs() {
  log "Preparing app directories"
  sudo install -d -o "$USER" -g "$USER" /opt/yuan-rate-limiter
  sudo install -d -o "$USER" -g "$USER" /var/log/yuan-rate-limiter
  sudo install -d -m 0755 /etc/yuan-rate-limiter

  sudo tee /etc/yuan-rate-limiter/node.env >/dev/null <<EOF
ROLE=$ROLE
PUBLIC_IP=$PUBLIC_IP
PRIVATE_IP=$PRIVATE_IP
APP_PORT=$APP_PORT
REDIS_HOST=$REDIS_HOST
REDIS_PORT=$REDIS_PORT
REDIS_PASSWORD=$REDIS_PASSWORD
EOF
}

verify() {
  log "Verification"
  echo "role=$ROLE"
  echo "public_ip=$PUBLIC_IP"
  echo "private_ip=$PRIVATE_IP"
  hostnamectl
  echo "cpu=$(nproc)"
  free -h
  df -h /
  dotnet --list-sdks
  dotnet --list-runtimes
  systemctl is-active prometheus-node-exporter || true
}

main() {
  require_sudo
  install_common_packages
  tune_host
  install_dotnet
  prepare_app_dirs
  verify
  log "Done"
}

main "$@"
