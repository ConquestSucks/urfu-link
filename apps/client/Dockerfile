FROM node:22.14.0-alpine AS base
ENV PNPM_HOME=/pnpm
ENV PATH=$PNPM_HOME:$PATH
RUN corepack enable
WORKDIR /workspace

FROM base AS build
COPY .npmrc package.json pnpm-workspace.yaml pnpm-lock.yaml ./
COPY apps/client/package.json apps/client/package.json
COPY packages/api-client/package.json packages/api-client/package.json
COPY packages/ui/package.json packages/ui/package.json
RUN pnpm install --frozen-lockfile
COPY . .
RUN pnpm --filter @urfu-link/client web:build

FROM nginxinc/nginx-unprivileged:1.29-alpine AS final
COPY apps/client/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /workspace/apps/client/dist /usr/share/nginx/html
COPY apps/client/docker-entrypoint.sh /docker-entrypoint.sh
USER root
RUN chmod +x /docker-entrypoint.sh && chown -R 101:101 /usr/share/nginx/html
USER 101
EXPOSE 8080
ENTRYPOINT ["/docker-entrypoint.sh"]
