FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:alpine AS production
RUN apk add --no-cache iputils net-tools busybox-extras wget curl vim bind-tools netcat-openbsd iproute2 tcpdump
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8601
CMD ["nginx", "-g", "daemon off;"]
