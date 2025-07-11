# Railway Deployment Guide for DOAMapper

## Prerequisites

1. **Railway Account**: Sign up at [railway.app](https://railway.app)
2. **GitHub Repository**: Your code should be in a GitHub repository
3. **Railway CLI** (optional): Install from [docs.railway.app/develop/cli](https://docs.railway.app/develop/cli)

## Step-by-Step Deployment

### 1. Create New Railway Project

1. Go to [railway.app](https://railway.app) and log in
2. Click **"New Project"**
3. Select **"Deploy from GitHub repo"**
4. Choose your DOAMapper repository
5. Railway will automatically detect the Dockerfile and start building

### 2. Add PostgreSQL Database (CRITICAL STEP)

**This is what you're missing!** Railway doesn't automatically create a database - you need to add it as a separate service.

1. In your Railway project dashboard, click **"+ New"**
2. Select **"Database"**
3. Choose **"Add PostgreSQL"**
4. Railway will create a PostgreSQL instance and automatically set the `DATABASE_URL` environment variable

### 3. Configure Environment Variables

Railway automatically provides these variables:
- `PORT` - Dynamic port (handled automatically)
- `DATABASE_URL` - PostgreSQL connection string (set when you add PostgreSQL)
- `ASPNETCORE_ENVIRONMENT` - Set to "Production" in Dockerfile

### 4. Deploy and Monitor

1. After adding PostgreSQL, your app should automatically redeploy
2. Monitor the deployment logs in Railway dashboard
3. Once deployed, Railway will provide a public URL

## Troubleshooting

### Database Connection Issues

**Error**: `Format of the initialization string does not conform to specification`

**Solution**: This means PostgreSQL database is not set up. Follow Step 2 above.

### Memory Issues with Large Imports

If you encounter memory issues with 90MB imports:
1. Upgrade to Railway's paid plan for more memory
2. Consider implementing chunked file uploads

### Build Failures

1. Check the build logs in Railway dashboard
2. Ensure all required files are committed to Git
3. Verify Dockerfile syntax

## Railway Project Structure

After setup, your Railway project should have:
```
├── doamapper (your app service)
└── postgresql (database service)
```

## Environment Variables Reference

| Variable | Source | Purpose |
|----------|--------|---------|
| `PORT` | Railway | Dynamic port assignment |
| `DATABASE_URL` | PostgreSQL service | Database connection |
| `ASPNETCORE_ENVIRONMENT` | Dockerfile | Set to "Production" |

## Cost Estimation

- **Free Tier**: 500 hours/month, $5 credit
- **PostgreSQL**: Included in free tier
- **Upgrade**: $5/month for more resources if needed

## Post-Deployment

1. **Test the application** using the Railway-provided URL
2. **Import test data** to verify large file handling works
3. **Monitor resource usage** in Railway dashboard
4. **Set up custom domain** (optional, requires paid plan)

## Support

- Railway Documentation: [docs.railway.app](https://docs.railway.app)
- Railway Discord: [discord.gg/railway](https://discord.gg/railway)
- Railway Status: [status.railway.app](https://status.railway.app)
