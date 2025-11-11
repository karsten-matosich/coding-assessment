FROM alpine:3.20

RUN apk add --no-cache sqlite && rm -rf /var/cache/apk/*

WORKDIR /usr/src/app

COPY init.sql .

# Create an initialization script that runs on container start
RUN echo '#!/bin/sh' > /usr/local/bin/init-db.sh && \
    echo 'if [ ! -f /data/KarstenAssessmentDb.db ]; then' >> /usr/local/bin/init-db.sh && \
    echo '  sqlite3 /data/KarstenAssessmentDb.db < /usr/src/app/init.sql || exit 1' >> /usr/local/bin/init-db.sh && \
    echo 'fi' >> /usr/local/bin/init-db.sh && \
    chmod +x /usr/local/bin/init-db.sh

VOLUME /data

CMD ["sh", "-c", "/usr/local/bin/init-db.sh && tail -f /dev/null"]