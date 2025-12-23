# 1. preparation
copy a db backup file to e.g. C:\Daten\ipath_podman\backup 

make sure that inside compose.yml that backup folder is mapped to /opt/backup
```
    volumes:
      - mysql-data:/var/lib/mysql
      - C:/Daten/ipath_podman/backup:/opt/backup
```

# 1. create mysql docker
create and start docker container
```
podman compose up -d
```

# 2. connect and import data
```
docker exec -it ipath_mysql bash
```

inside docker 
```
gunzip < /opt/backup/ipath-20251222.sql.gz | mysql -uroot -p ipath_old
```

# 4. grant access to mysql from outside the container
```
CREATE USER 'import'@'%' IDENTIFIED BY '1mp0rt';
GRANT ALL ON ipath_old.* TO 'import'@'%';
FLUSH PRIVILEGES;
```

now connect from mywql workbench