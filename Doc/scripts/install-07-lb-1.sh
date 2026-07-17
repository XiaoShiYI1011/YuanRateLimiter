#!/usr/bin/env bash
set -euo pipefail

ROLE="lb-1"
PUBLIC_IP="${PUBLIC_IP:-REDACTED_PUBLIC_IP}"
PRIVATE_IP="${PRIVATE_IP:-REDACTED_PRIVATE_IP}"

APP_1="${APP_1:?please set APP_1 before running this script, for example 10.0.0.11:5001}"
APP_2="${APP_2:?please set APP_2 before running this script, for example 10.0.0.12:5001}"
APP_3="${APP_3:?please set APP_3 before running this script, for example 10.0.0.13:5001}"

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
  log "Applying lb-node kernel limits"
  sudo tee /etc/sysctl.d/99-yuan-rate-limiter.conf >/dev/null <<'SYSCTL'
net.core.somaxconn = 8192
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
www-data soft nofile 1048576
www-data hard nofile 1048576
LIMITS
}

install_nginx() {
  log "Installing and configuring Nginx"
  sudo apt-get install -y nginx
  sudo rm -f /etc/nginx/sites-enabled/default

  sudo tee /etc/nginx/conf.d/yuan-rate-limiter.conf >/dev/null <<NGINX
upstream yuan_rate_limiter_apps {
    least_conn;
    server ${APP_1} max_fails=3 fail_timeout=10s;
    server ${APP_2} max_fails=3 fail_timeout=10s;
    server ${APP_3} max_fails=3 fail_timeout=10s;
}

server {
    listen 80;
    server_name _;

    access_log /var/log/nginx/yuan-rate-limiter.access.log;
    error_log /var/log/nginx/yuan-rate-limiter.error.log warn;

    location = /health-lb {
        add_header Content-Type text/plain;
        return 200 "ok\\n";
    }

    location / {
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_set_header Connection "";
        proxy_connect_timeout 3s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
        proxy_next_upstream error timeout http_502 http_503 http_504;
        proxy_pass http://yuan_rate_limiter_apps;
    }
}
NGINX

  sudo nginx -t
  sudo systemctl enable nginx
  sudo systemctl restart nginx
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
  nginx -v
  curl -fsS http://127.0.0.1/health-lb
  ss -lntp | grep ':80' || true
  systemctl is-active nginx || true
  systemctl is-active prometheus-node-exporter || true
}

main() {
  require_sudo
  install_common_packages
  tune_host
  install_nginx
  verify
  log "Done"
}

main "$@"
