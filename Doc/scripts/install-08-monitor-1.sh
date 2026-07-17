#!/usr/bin/env bash
set -euo pipefail

ROLE="monitor-1"
PUBLIC_IP="${PUBLIC_IP:-REDACTED_PUBLIC_IP}"
PRIVATE_IP="${PRIVATE_IP:-REDACTED_PRIVATE_IP}"
NODE_EXPORTER_TARGETS="${NODE_EXPORTER_TARGETS:?please set NODE_EXPORTER_TARGETS as comma-separated host:9100 list}"

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
  log "Applying monitor-node kernel limits"
  sudo tee /etc/sysctl.d/99-yuan-rate-limiter.conf >/dev/null <<'SYSCTL'
net.core.somaxconn = 4096
net.ipv4.ip_local_port_range = 1024 65535
net.ipv4.tcp_tw_reuse = 1
fs.file-max = 1048576
SYSCTL
  sudo sysctl --system >/dev/null || true
}

install_prometheus() {
  log "Installing Prometheus"
  sudo apt-get install -y prometheus
  if [[ ! -f /etc/prometheus/prometheus.yml.bak-yuan-rate-limiter ]]; then
    sudo cp /etc/prometheus/prometheus.yml /etc/prometheus/prometheus.yml.bak-yuan-rate-limiter
  fi

  {
    cat <<'YAML'
global:
  scrape_interval: 5s
  evaluation_interval: 5s

scrape_configs:
  - job_name: "node-exporter"
    static_configs:
      - targets:
YAML
    IFS=',' read -ra targets <<< "$NODE_EXPORTER_TARGETS"
    for target in "${targets[@]}"; do
      target="$(echo "$target" | xargs)"
      [[ -n "$target" ]] && printf '          - "%s"\n' "$target"
    done
  } | sudo tee /etc/prometheus/prometheus.yml >/dev/null

  sudo systemctl enable prometheus
  sudo systemctl restart prometheus
}

prepare_dirs() {
  log "Preparing report directories"
  sudo install -d -o "$USER" -g "$USER" /opt/yuan-rate-limiter-monitor
  sudo install -d -o "$USER" -g "$USER" /var/log/yuan-rate-limiter-monitor
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
  prometheus --version | head -n 1 || true
  curl -fsS http://127.0.0.1:9090/-/ready || true
  ss -lntp | grep ':9090' || true
  systemctl is-active prometheus || true
  systemctl is-active prometheus-node-exporter || true
}

main() {
  require_sudo
  install_common_packages
  tune_host
  install_prometheus
  prepare_dirs
  verify
  log "Done"
}

main "$@"
