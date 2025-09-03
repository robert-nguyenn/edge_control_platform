import React, { useState, useEffect } from 'react'
import {
  AppBar,
  Toolbar,
  Typography,
  Container,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  IconButton,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  Slider,
  Box,
  Chip,
  List,
  ListItem,
  ListItemText,
  Alert,
  CircularProgress
} from '@mui/material'
import { Edit, History, Save, Cancel } from '@mui/icons-material'
import axios from 'axios'

interface Flag {
  key: string
  description: string
  rolloutPercent: number
  updatedAt: string
}

interface AuditEntry {
  id: number
  flagKey: string
  action: string
  actor: string
  payloadHash: string
  createdAt: string
}

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000'
const API_KEY = import.meta.env.VITE_API_KEY || 'demo-key'

const axiosInstance = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Authorization': `Bearer ${API_KEY}`,
    'Content-Type': 'application/json'
  }
})

function App() {
  const [flags, setFlags] = useState<Flag[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [editingFlag, setEditingFlag] = useState<Flag | null>(null)
  const [auditDialogOpen, setAuditDialogOpen] = useState(false)
  const [auditEntries, setAuditEntries] = useState<AuditEntry[]>([])
  const [selectedFlagKey, setSelectedFlagKey] = useState<string>('')

  useEffect(() => {
    loadFlags()
  }, [])

  const loadFlags = async () => {
    try {
      setLoading(true)
      const response = await axiosInstance.get('/flags')
      setFlags(response.data)
      setError(null)
    } catch (err) {
      setError('Failed to load flags')
      console.error('Error loading flags:', err)
    } finally {
      setLoading(false)
    }
  }

  const updateFlag = async (flagKey: string, updates: Partial<Flag>) => {
    try {
      await axiosInstance.put(`/flags/${flagKey}`, updates)
      await loadFlags()
      setEditingFlag(null)
    } catch (err) {
      setError('Failed to update flag')
      console.error('Error updating flag:', err)
    }
  }

  const loadAuditLog = async (flagKey: string) => {
    try {
      const response = await axiosInstance.get(`/audit?flag=${flagKey}&limit=10`)
      setAuditEntries(response.data)
      setSelectedFlagKey(flagKey)
      setAuditDialogOpen(true)
    } catch (err) {
      setError('Failed to load audit log')
      console.error('Error loading audit log:', err)
    }
  }

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleString()
  }

  const getActionColor = (action: string) => {
    switch (action) {
      case 'create': return 'success'
      case 'update': return 'primary'
      case 'shadow_mismatch': return 'warning'
      default: return 'default'
    }
  }

  if (loading) {
    return (
      <Box display="flex" justifyContent="center" alignItems="center" minHeight="100vh">
        <CircularProgress />
      </Box>
    )
  }

  return (
    <div>
      <AppBar position="static">
        <Toolbar>
          <Typography variant="h6" component="div" sx={{ flexGrow: 1 }}>
            🚀 Edge Control Platform - Admin
          </Typography>
          <Button color="inherit" onClick={loadFlags}>
            Refresh
          </Button>
        </Toolbar>
      </AppBar>

      <Container maxWidth="lg" sx={{ mt: 4, mb: 4 }}>
        {error && (
          <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
            {error}
          </Alert>
        )}

        <Paper sx={{ p: 3 }}>
          <Typography variant="h5" gutterBottom>
            Feature Flags
          </Typography>
          
          <TableContainer>
            <Table>
              <TableHead>
                <TableRow>
                  <TableCell>Flag Key</TableCell>
                  <TableCell>Description</TableCell>
                  <TableCell>Rollout %</TableCell>
                  <TableCell>Last Updated</TableCell>
                  <TableCell>Actions</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {flags.map((flag) => (
                  <TableRow key={flag.key}>
                    <TableCell>
                      <Typography variant="body2" fontFamily="monospace">
                        {flag.key}
                      </Typography>
                    </TableCell>
                    <TableCell>{flag.description}</TableCell>
                    <TableCell>
                      <Chip 
                        label={`${flag.rolloutPercent}%`}
                        color={flag.rolloutPercent > 0 ? 'primary' : 'default'}
                        variant={flag.rolloutPercent > 0 ? 'filled' : 'outlined'}
                      />
                    </TableCell>
                    <TableCell>{formatDate(flag.updatedAt)}</TableCell>
                    <TableCell>
                      <IconButton 
                        size="small" 
                        onClick={() => setEditingFlag(flag)}
                        title="Edit flag"
                      >
                        <Edit />
                      </IconButton>
                      <IconButton 
                        size="small" 
                        onClick={() => loadAuditLog(flag.key)}
                        title="View audit log"
                      >
                        <History />
                      </IconButton>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </Paper>
      </Container>

      {/* Edit Flag Dialog */}
      <Dialog open={!!editingFlag} onClose={() => setEditingFlag(null)} maxWidth="sm" fullWidth>
        <DialogTitle>Edit Flag: {editingFlag?.key}</DialogTitle>
        <DialogContent>
          <Box sx={{ pt: 2 }}>
            <TextField
              label="Description"
              fullWidth
              value={editingFlag?.description || ''}
              onChange={(e) => 
                setEditingFlag(prev => prev ? {...prev, description: e.target.value} : null)
              }
              sx={{ mb: 3 }}
            />
            
            <Typography gutterBottom>
              Rollout Percentage: {editingFlag?.rolloutPercent || 0}%
            </Typography>
            <Slider
              value={editingFlag?.rolloutPercent || 0}
              onChange={(_, value) => 
                setEditingFlag(prev => prev ? {...prev, rolloutPercent: value as number} : null)
              }
              marks={[
                { value: 0, label: '0%' },
                { value: 25, label: '25%' },
                { value: 50, label: '50%' },
                { value: 75, label: '75%' },
                { value: 100, label: '100%' }
              ]}
              step={5}
              min={0}
              max={100}
              valueLabelDisplay="auto"
            />
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEditingFlag(null)} startIcon={<Cancel />}>
            Cancel
          </Button>
          <Button 
            onClick={() => editingFlag && updateFlag(editingFlag.key, {
              description: editingFlag.description,
              rolloutPercent: editingFlag.rolloutPercent
            })}
            variant="contained"
            startIcon={<Save />}
          >
            Save
          </Button>
        </DialogActions>
      </Dialog>

      {/* Audit Log Dialog */}
      <Dialog open={auditDialogOpen} onClose={() => setAuditDialogOpen(false)} maxWidth="md" fullWidth>
        <DialogTitle>Audit Log: {selectedFlagKey}</DialogTitle>
        <DialogContent>
          <List>
            {auditEntries.map((entry) => (
              <ListItem key={entry.id} divider>
                <ListItemText
                  primary={
                    <Box display="flex" alignItems="center" gap={1}>
                      <Chip 
                        label={entry.action} 
                        size="small" 
                        color={getActionColor(entry.action) as any}
                      />
                      <Typography variant="body2">
                        by {entry.actor}
                      </Typography>
                    </Box>
                  }
                  secondary={
                    <Box>
                      <Typography variant="body2" color="text.secondary">
                        {formatDate(entry.createdAt)}
                      </Typography>
                      <Typography variant="body2" fontFamily="monospace" color="text.secondary">
                        Hash: {entry.payloadHash}
                      </Typography>
                    </Box>
                  }
                />
              </ListItem>
            ))}
            {auditEntries.length === 0 && (
              <ListItem>
                <ListItemText primary="No audit entries found" />
              </ListItem>
            )}
          </List>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAuditDialogOpen(false)}>Close</Button>
        </DialogActions>
      </Dialog>
    </div>
  )
}

export default App
