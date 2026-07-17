#!/usr/bin/env bash
set -euo pipefail

ROLE="redis-1"
PUBLIC_IP="${PUBLIC_IP:-REDACTED_PUBLIC_IP}"
PRIVATE_IP="${PRIVATE_IP:?please set PRIVATE_IP before running this script}"
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
    ca-certificates curl wget gnupg lsb-release unzip git jq htop \
    net-tools iproute2 sysstat iftop iotop tar vim \
    prometheus-node-exporter

  sudo timedatectl set-timezone Asia/Shanghai || true
  sudo systemctl enable --now sysstat || true
  sudo systemctl enable --now prometheus-node-exporter || true
}

tune_host() {
  log "Applying redis-node kernel limits"
  sudo tee /etc/sysctl.d/99-yuan-rate-limiter.conf >/dev/null <<'SYSCTL'
net.core.somaxconn = 4096
net.ipv4.ip_local_port_range = 1024 65535
net.ipv4.tcp_tw_reuse = 1
fs.file-max = 1048576
vm.overcommit_memory = 1
SYSCTL
  sudo sysctl --system >/dev/null || true

  sudo tee /etc/security/limits.d/99-yuan-rate-limiter.conf >/dev/null <<'LIMITS'
* soft nofile 1048576
* hard nofile 1048576
root soft nofile 1048576
root hard nofile 1048576
redis soft nofile 1048576
redis hard nofile 1048576
LIMITS
}

set_redis_config() {
  local key="$1"
  local value="$2"
  if sudo grep -Eq "^[#[:space:]]*${key}[[:space:]]+" /etc/redis/redis.conf; then
    sudo sed -i -E "s|^[#[:space:]]*${key}[[:space:]]+.*|${key} ${value}|" /etc/redis/redis.conf
  else
    echo "${key} ${value}" | sudo tee -a /etc/redis/redis.conf >/dev/null
  fi
}

install_redis() {
  log "Installing Redis"
  sudo apt-get install -y redis-server redis-tools
  if [[ ! -f /etc/redis/redis.conf.bak-yuan-rate-limiter ]]; then
    sudo cp /etc/redis/redis.conf /etc/redis/redis.conf.bak-yuan-rate-limiter
  fi

  set_redis_config "bind" "127.0.0.1 ${PRIVATE_IP}"
  set_redis_config "protected-mode" "yes"
  set_redis_config "port" "${REDIS_PORT}"
  set_redis_config "requirepass" "${REDIS_PASSWORD}"
  set_redis_config "timeout" "0"
  set_redis_config "tcp-keepalive" "60"
  set_redis_config "databases" "16"
  set_redis_config "maxmemory" "1200mb"
  set_redis_config "maxmemory-policy" "noeviction"
  set_redis_config "appendonly" "no"

  sudo systemctl enable redis-server
  sudo systemctl restart redis-server
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
  export REDISCLI_AUTH="$REDIS_PASSWORD"
  redis-cli -h 127.0.0.1 -p "$REDIS_PORT" ping
  redis-cli -h "$PRIVATE_IP" -p "$REDIS_PORT" ping
  ss -lntp | grep ":${REDIS_PORT}" || true
  systemctl is-active redis-server || true
  systemctl is-active prometheus-node-exporter || true
}

main() {
  require_sudo
  install_common_packages
  tune_host
  install_redis
  verify
  log "Done"
}

main "$@"
