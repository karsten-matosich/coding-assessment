FROM node:20-alpine AS build

WORKDIR /app

COPY ["karsten-assessment-angular-app/package.json", "karsten-assessment-angular-app/package-lock.json", "./"]
RUN npm ci

COPY ["karsten-assessment-angular-app/", "./"]

RUN npx ng build --configuration=production

FROM nginx:alpine

COPY --from=build /app/dist/karsten-assessment-angular-app/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]