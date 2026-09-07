# Rebuild the upstream gosu 1.19 release with a patched Go standard library.
FROM --platform=$BUILDPLATFORM golang:1.26.8-alpine AS gosu
ARG TARGETARCH
WORKDIR /src
RUN apk add --no-cache git && \
    git init && \
    git fetch --depth 1 https://github.com/tianon/gosu.git 6456aaa0f3c854d199d0f037f068eb97515b7513 && \
    git checkout --detach FETCH_HEAD
RUN CGO_ENABLED=0 GOOS=linux GOARCH=$TARGETARCH go build -trimpath -o /gosu .

FROM postgres:18-alpine
RUN apk upgrade --no-cache
COPY --from=gosu /gosu /usr/local/bin/gosu
RUN gosu --version && test "$(gosu postgres id -u)" = "$(id -u postgres)"

COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

ENTRYPOINT ["/entrypoint.sh"]
