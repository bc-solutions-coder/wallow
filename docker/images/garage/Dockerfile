FROM dxflrs/garage:v2.2.0 AS garage

FROM alpine:3.21
RUN apk add --no-cache gettext
COPY --from=garage /garage /usr/local/bin/garage
COPY garage.toml.template /etc/garage/garage.toml.template
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

# Rendered from the template at startup; also lets an exec'd CLI (healthchecks)
# find the same config the server is using.
ENV GARAGE_CONFIG_FILE=/etc/garage.toml

VOLUME ["/var/lib/garage/meta", "/var/lib/garage/data"]

ENTRYPOINT ["/entrypoint.sh"]
