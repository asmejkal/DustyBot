FROM kibana:7.9.2

RUN kibana-plugin install https://github.com/sivasamyk/logtrail/releases/download/v0.1.31/logtrail-7.9.2-0.1.31.zip

WORKDIR /
COPY logtrail.json /usr/share/kibana/plugins/logtrail/logtrail.json