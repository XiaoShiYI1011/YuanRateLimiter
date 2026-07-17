#!/usr/bin/env bash
set -euo pipefail

ROLE="loadgen-1"
PUBLIC_IP="${PUBLIC_IP:-REDACTED_PUBLIC_IP}"
PRIVATE_IP="${PRIVATE_IP:-REDACTED_PRIVATE_IP}"

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
    ca-certificates curl wget gnupg lsb-release unzip git jq htop \
    net-tools iproute2 sysstat iftop iotop tar vim \
    prometheus-node-exporter

  sudo timedatectl set-timezone Asia/Shanghai || true
  sudo systemctl enable --now sysstat || true
  sudo systemctl enable --now prometheus-node-exporter || true
}

tune_host() {
  log "Applying lightweight pressure-test kernel limits"
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

install_k6() {
  if command -v k6 >/dev/null 2>&1; then
    log "k6 already installed: $(k6 version)"
    return
  fi

  log "Installing k6"
  sudo install -d -m 0755 /usr/share/keyrings
  curl -fsSL https://dl.k6.io/key.gpg | sudo gpg --dearmor -o /usr/share/keyrings/k6-archive-keyring.gpg
  echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" | sudo tee /etc/apt/sources.list.d/k6.list >/dev/null
  sudo apt-get update
  sudo apt-get install -y k6
}

prepare_dirs() {
  log "Preparing pressure-test directories"
  sudo install -d -o "$USER" -g "$USER" /opt/yuan-rate-limiter-loadtest
  sudo install -d -o "$USER" -g "$USER" /var/log/yuan-rate-limiter-loadtest
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
  k6 version
  systemctl is-active prometheus-node-exporter || true
}

main() {
  require_sudo
  install_common_packages
  tune_host
  install_k6
  prepare_dirs
  verify
  log "Done"
}

main "$@"
