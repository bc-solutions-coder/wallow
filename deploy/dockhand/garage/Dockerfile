FROM dxflrs/garage:v2.2.0 AS garage
FROM alpine:3.21
RUN apk add --no-cache gettext
COPY --from=garage /garage /usr/local/bin/garage
